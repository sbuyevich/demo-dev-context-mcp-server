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

    private readonly Mock<ICityService> _cityService =
        new(MockBehavior.Strict);
    private readonly Mock<IOpenMeteoClient> _openMeteoClient =
        new(MockBehavior.Strict);
    private readonly Mock<IGeocodingCacheRepository> _cacheRepository =
        new(MockBehavior.Strict);
    private readonly Mock<TimeProvider> _timeProvider =
        new(MockBehavior.Strict);
    private readonly Mock<ILogger<CityGeocodingService>> _logger =
        new(MockBehavior.Strict);
    private readonly CityGeocodingService _target;

    public CityGeocodingServiceTests()
    {
        _target = new CityGeocodingService(
            _cityService.Object,
            _openMeteoClient.Object,
            _cacheRepository.Object,
            _timeProvider.Object,
            _logger.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_BlankCity_ReturnsCityNotFound(
        string cityName)
    {
        // Purpose: Blank route values must be rejected before dependencies are used.
        // arrange

        // act
        var actual = await _target.GetAsync(cityName);

        // assert
        Assert.Equal(
            CityGeocodingOutcomeKind.CityNotFound,
            actual.Kind);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_UnrecognizedCity_ReturnsCityNotFound()
    {
        // Purpose: Unsupported cities must not access cache or upstream services.
        // arrange
        _cityService.Setup(service => service.GetCityNames())
            .Returns(["London", "New York"]);

        // act
        var actual = await _target.GetAsync("missing");

        // assert
        Assert.Equal(
            CityGeocodingOutcomeKind.CityNotFound,
            actual.Kind);
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsCanonicalCachedRecord()
    {
        // Purpose: A canonical cache hit must bypass Open-Meteo and preserve cancellation.
        // arrange
        var cancellationToken = new CancellationTokenSource().Token;
        var expected = CreateRecord();
        _cityService.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        _cacheRepository.Setup(repository =>
                repository.GetAsync("NEW YORK", cancellationToken))
            .ReturnsAsync(expected);

        // act
        var actual = await _target.GetAsync(
            "  nEw YoRk  ",
            cancellationToken);

        // assert
        Assert.Equal(CityGeocodingOutcomeKind.Success, actual.Kind);
        Assert.Same(expected, actual.Record);
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                "NEW YORK",
                cancellationToken),
            Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_CacheMissWithExactMatch_PersistsFirstMatch()
    {
        // Purpose: A miss must select the first exact upstream match and persist it.
        // arrange
        var cancellationToken = new CancellationTokenSource().Token;
        _cityService.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        _cacheRepository.Setup(repository =>
                repository.GetAsync("NEW YORK", cancellationToken))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                cancellationToken))
            .ReturnsAsync(
                Response(
                    Location("New York City", 1),
                    Location("new york", 2),
                    Location("New York", 3)));
        _timeProvider.Setup(provider => provider.GetUtcNow())
            .Returns(Now);
        _cacheRepository.Setup(repository => repository.UpsertAsync(
                It.Is<GeocodingCacheRecord>(record =>
                    record.NormalizedCityName == "NEW YORK"
                    && record.DisplayName == "New York"
                    && record.Country == "United States"
                    && record.Latitude == 40.7128
                    && record.Longitude == -74.006
                    && record.Population == 2
                    && record.RetrievedAtUtc == Now),
                cancellationToken))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetAsync(
            "new york",
            cancellationToken);

        // assert
        Assert.Equal(CityGeocodingOutcomeKind.Success, actual.Kind);
        Assert.Equal("NEW YORK", actual.Record!.NormalizedCityName);
        Assert.Equal("New York", actual.Record.DisplayName);
        Assert.Equal(2, actual.Record.Population);
        Assert.Equal(Now, actual.Record.RetrievedAtUtc);
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                "NEW YORK",
                cancellationToken),
            Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                cancellationToken),
            Times.Once);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.Is<GeocodingCacheRecord>(record =>
                    record.Population == 2),
                cancellationToken),
            Times.Once);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_NoExactUpstreamMatch_ReturnsGeocodingNotFound()
    {
        // Purpose: Similar upstream names must not be accepted as exact city matches.
        // arrange
        SetupCacheMiss();
        _openMeteoClient.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                CancellationToken.None))
            .ReturnsAsync(Response(Location("New York City", 1)));

        // act
        var actual = await _target.GetAsync("New York");

        // assert
        Assert.Equal(
            CityGeocodingOutcomeKind.GeocodingNotFound,
            actual.Kind);
        VerifyCacheMissCalls(CancellationToken.None);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                CancellationToken.None),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_TransportFailure_ReturnsServiceUnavailable()
    {
        // Purpose: Transport failures on cache misses must become unavailable outcomes.
        // arrange
        var expectedException =
            new HttpRequestException("unavailable");
        SetupCacheMiss();
        _openMeteoClient.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                CancellationToken.None))
            .ThrowsAsync(expectedException);
        SetupWarningLog(
            expectedException,
            "Open-Meteo transport failed for city New York");

        // act
        var actual = await _target.GetAsync("New York");

        // assert
        Assert.Equal(
            CityGeocodingOutcomeKind.ServiceUnavailable,
            actual.Kind);
        VerifyCacheMissCalls(CancellationToken.None);
        VerifyUpstreamCall(CancellationToken.None);
        VerifyWarningLog(
            expectedException,
            "Open-Meteo transport failed for city New York");
        VerifyNoUpsert();
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_ApiFailure_ReturnsServiceUnavailable()
    {
        // Purpose: Generated API failures must become unavailable outcomes and be logged.
        // arrange
        var expectedException = new ApiException(
            "bad gateway",
            502,
            "upstream body",
            new Dictionary<string, IEnumerable<string>>(),
            new InvalidOperationException());
        SetupCacheMiss();
        _openMeteoClient.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                CancellationToken.None))
            .ThrowsAsync(expectedException);
        SetupWarningLog(
            expectedException,
            "Open-Meteo returned an error for city New York");

        // act
        var actual = await _target.GetAsync("New York");

        // assert
        Assert.Equal(
            CityGeocodingOutcomeKind.ServiceUnavailable,
            actual.Kind);
        VerifyCacheMissCalls(CancellationToken.None);
        VerifyUpstreamCall(CancellationToken.None);
        VerifyWarningLog(
            expectedException,
            "Open-Meteo returned an error for city New York");
        VerifyNoUpsert();
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_TimeoutFailure_ReturnsServiceUnavailable()
    {
        // Purpose: Explicit upstream timeouts must become unavailable outcomes and be logged.
        // arrange
        var expectedException = new TimeoutException();
        SetupCacheMiss();
        _openMeteoClient.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                CancellationToken.None))
            .ThrowsAsync(expectedException);
        SetupWarningLog(
            expectedException,
            "Open-Meteo timed out for city New York");

        // act
        var actual = await _target.GetAsync("New York");

        // assert
        Assert.Equal(
            CityGeocodingOutcomeKind.ServiceUnavailable,
            actual.Kind);
        VerifyCacheMissCalls(CancellationToken.None);
        VerifyUpstreamCall(CancellationToken.None);
        VerifyWarningLog(
            expectedException,
            "Open-Meteo timed out for city New York");
        VerifyNoUpsert();
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_RequestCancellation_PropagatesCancellation()
    {
        // Purpose: Caller cancellation must propagate without being translated or logged.
        // arrange
        using var source = new CancellationTokenSource();
        SetupCacheMiss(source.Token);
        _openMeteoClient.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                source.Token))
            .ThrowsAsync(new OperationCanceledException(source.Token));
        source.Cancel();

        // act
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(
            () => _target.GetAsync("New York", source.Token));

        // assert
        Assert.Equal(source.Token, actual.CancellationToken);
        VerifyCacheMissCalls(source.Token);
        VerifyUpstreamCall(source.Token);
        VerifyNoUpsert();
        VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetAsync_PersistenceFailure_PropagatesFailure()
    {
        // Purpose: Cache persistence failures must remain internal failures for the API layer.
        // arrange
        var expectedException =
            new InvalidOperationException("database");
        SetupCacheMiss();
        _openMeteoClient.Setup(client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                CancellationToken.None))
            .ReturnsAsync(Response(Location("New York", 1)));
        _timeProvider.Setup(provider => provider.GetUtcNow())
            .Returns(Now);
        _cacheRepository.Setup(repository => repository.UpsertAsync(
                It.Is<GeocodingCacheRecord>(record =>
                    record.NormalizedCityName == "NEW YORK"
                    && record.Population == 1),
                CancellationToken.None))
            .ThrowsAsync(expectedException);

        // act
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _target.GetAsync("New York"));

        // assert
        Assert.Same(expectedException, actual);
        VerifyCacheMissCalls(CancellationToken.None);
        VerifyUpstreamCall(CancellationToken.None);
        _timeProvider.Verify(
            provider => provider.GetUtcNow(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.Is<GeocodingCacheRecord>(record =>
                    record.NormalizedCityName == "NEW YORK"
                    && record.Population == 1),
                CancellationToken.None),
            Times.Once);
        VerifyNoOtherCalls();
    }

    private void SetupCacheMiss(
        CancellationToken cancellationToken = default)
    {
        _cityService.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        _cacheRepository.Setup(repository =>
                repository.GetAsync("NEW YORK", cancellationToken))
            .ReturnsAsync((GeocodingCacheRecord?)null);
    }

    private void VerifyCacheMissCalls(
        CancellationToken cancellationToken)
    {
        _cityService.Verify(
            service => service.GetCityNames(),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(
                "NEW YORK",
                cancellationToken),
            Times.Once);
    }

    private void VerifyUpstreamCall(
        CancellationToken cancellationToken) =>
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                cancellationToken),
            Times.Once);

    private void VerifyNoUpsert() =>
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

    private void SetupWarningLog(
        Exception exception,
        string message) =>
        _logger.Setup(logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>(
                    (value, _) => value.ToString() == message),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()));

    private void VerifyWarningLog(
        Exception exception,
        string message) =>
        _logger.Verify(logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>(
                    (value, _) => value.ToString() == message),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

    private void VerifyNoOtherCalls()
    {
        _cityService.VerifyNoOtherCalls();
        _openMeteoClient.VerifyNoOtherCalls();
        _cacheRepository.VerifyNoOtherCalls();
        _timeProvider.VerifyNoOtherCalls();
        _logger.VerifyNoOtherCalls();
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
