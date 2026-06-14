using Demo.Cities;
using Microsoft.Extensions.Logging;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Geocoding;

namespace STI.City.Tests.Core;

public sealed class CityGeocodingServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICityService> _cities = new(MockBehavior.Strict);
    private readonly Mock<IOpenMeteoClient> _client =
        new(MockBehavior.Strict);
    private readonly Mock<IGeocodingCacheRepository> _repository =
        new(MockBehavior.Strict);
    private readonly Mock<TimeProvider> _timeProvider =
        new(MockBehavior.Strict);
    private readonly Mock<ILogger<CityGeocodingService>> _logger = new();

    [Fact]
    public async Task UnknownCityDoesNotUseCacheOrUpstream()
    {
        _cities.Setup(service => service.GetCityNames())
            .Returns(["London", "New York"]);
        var service = CreateService();

        var outcome = await service.GetAsync("missing");

        Assert.Equal(
            CityGeocodingOutcomeKind.CityNotFound,
            outcome.Kind);
        _repository.VerifyNoOtherCalls();
        _client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankCityDoesNotUseCollaborators(string cityName)
    {
        var service = CreateService();

        var outcome = await service.GetAsync(cityName);

        Assert.Equal(
            CityGeocodingOutcomeKind.CityNotFound,
            outcome.Kind);
        _cities.VerifyNoOtherCalls();
        _repository.VerifyNoOtherCalls();
        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CacheHitUsesCanonicalNormalizedName()
    {
        var token = new CancellationTokenSource().Token;
        var record = CreateRecord();
        _cities.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        _repository.Setup(repository =>
                repository.GetAsync("NEW YORK", token))
            .ReturnsAsync(record);
        var service = CreateService();

        var outcome = await service.GetAsync("  nEw YoRk  ", token);

        Assert.Equal(CityGeocodingOutcomeKind.Success, outcome.Kind);
        Assert.Same(record, outcome.Record);
        _client.VerifyNoOtherCalls();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task CacheMissSelectsFirstExactMatchAndPersists()
    {
        var token = new CancellationTokenSource().Token;
        _cities.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        _repository.Setup(repository =>
                repository.GetAsync("NEW YORK", token))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _client.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                token))
            .ReturnsAsync(
                Response(
                    Location("New York City", 1),
                    Location("new york", 2),
                    Location("New York", 3)));
        _timeProvider.Setup(provider => provider.GetUtcNow())
            .Returns(Now);
        GeocodingCacheRecord? persisted = null;
        _repository.Setup(repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                token))
            .Callback<GeocodingCacheRecord, CancellationToken>(
                (record, _) => persisted = record)
            .Returns(Task.CompletedTask);
        var service = CreateService();

        var outcome = await service.GetAsync("new york", token);

        Assert.Equal(CityGeocodingOutcomeKind.Success, outcome.Kind);
        Assert.Equal(2, outcome.Record!.Population);
        Assert.Equal("New York", outcome.Record.DisplayName);
        Assert.Equal("NEW YORK", outcome.Record.NormalizedCityName);
        Assert.Equal(Now, outcome.Record.RetrievedAtUtc);
        Assert.Equal(outcome.Record, persisted);
        _client.VerifyAll();
        _repository.VerifyAll();
    }

    [Fact]
    public async Task NoExactUpstreamMatchReturnsNotFound()
    {
        SetupCacheMiss();
        _client.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("New York City", 1)));
        var service = CreateService();

        var outcome = await service.GetAsync("New York");

        Assert.Equal(
            CityGeocodingOutcomeKind.GeocodingNotFound,
            outcome.Kind);
        _repository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpstreamFailureReturnsServiceUnavailable()
    {
        SetupCacheMiss();
        _client.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unavailable"));
        var service = CreateService();

        var outcome = await service.GetAsync("New York");

        Assert.Equal(
            CityGeocodingOutcomeKind.ServiceUnavailable,
            outcome.Kind);
    }

    [Fact]
    public async Task ApiFailureReturnsServiceUnavailable()
    {
        SetupCacheMiss();
        _client.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new ApiException(
                    "bad gateway",
                    502,
                    "upstream body",
                    new Dictionary<string, IEnumerable<string>>(),
                    new InvalidOperationException()));
        var service = CreateService();

        var outcome = await service.GetAsync("New York");

        Assert.Equal(
            CityGeocodingOutcomeKind.ServiceUnavailable,
            outcome.Kind);
    }

    [Fact]
    public async Task TimeoutReturnsServiceUnavailable()
    {
        SetupCacheMiss();
        _client.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());
        var service = CreateService();

        var outcome = await service.GetAsync("New York");

        Assert.Equal(
            CityGeocodingOutcomeKind.ServiceUnavailable,
            outcome.Kind);
    }

    [Fact]
    public async Task RequestCancellationPropagates()
    {
        var source = new CancellationTokenSource();
        SetupCacheMiss(source.Token);
        _client.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                source.Token))
            .ThrowsAsync(new OperationCanceledException(source.Token));
        var service = CreateService();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetAsync("New York", source.Token));
    }

    [Fact]
    public async Task PersistenceFailurePropagates()
    {
        SetupCacheMiss();
        _client.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response(Location("New York", 1)));
        _timeProvider.Setup(provider => provider.GetUtcNow())
            .Returns(Now);
        _repository.Setup(repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database"));
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAsync("New York"));
    }

    private CityGeocodingService CreateService() =>
        new(
            _cities.Object,
            _client.Object,
            _repository.Object,
            _timeProvider.Object,
            _logger.Object);

    private void SetupCacheMiss(
        CancellationToken cancellationToken = default)
    {
        _cities.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        _repository.Setup(repository =>
                repository.GetAsync("NEW YORK", cancellationToken))
            .ReturnsAsync((GeocodingCacheRecord?)null);
    }

    private static GeocodingCacheRecord CreateRecord() =>
        new(
            "NEW YORK",
            "New York",
            "United States",
            40.7128,
            -74.006,
            8_804_190,
            Now);

    private static GeocodingResponse Response(
        params LocationResult[] results) =>
        new() { Results = results };

    private static LocationResult Location(
        string name,
        int? population) =>
        new()
        {
            Name = name,
            Country = "United States",
            Latitude = 40.7128,
            Longitude = -74.006,
            Population = population
        };
}
