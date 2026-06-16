using Demo.Cities;
using Microsoft.Extensions.Logging;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Core.Services;

namespace STI.City.Tests.Services;

public sealed class CityGeocodingServiceTests
{
    private const string Canonical = "New York";
    private const string NormalizedKey = "NEW YORK";
    private static readonly DateTimeOffset RetrievedAtUtc = new(2026, 6, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICityService> _cityService = new();
    private readonly Mock<IOpenMeteoClient> _openMeteoClient = new();
    private readonly Mock<IGeocodingCacheRepository> _cacheRepository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<ILogger<CityGeocodingService>> _logger = new();
    private readonly CityGeocodingService _target;

    public CityGeocodingServiceTests()
    {
        _timeProvider.Setup(clock => clock.GetUtcNow()).Returns(RetrievedAtUtc);
        _target = new CityGeocodingService(
            _cityService.Object,
            _openMeteoClient.Object,
            _cacheRepository.Object,
            _timeProvider.Object,
            _logger.Object);
    }

    // Purpose: blank route values are rejected before any city resolution
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetCityGeocodingAsync_BlankInput_ReturnsCityNotFoundWithoutResolvingCities(string input)
    {
        // arrange

        // act
        var actual = await _target.GetCityGeocodingAsync(input);

        // assert
        Assert.Equal(CityGeocodingStatus.CityNotFound, actual.Status);
        _cityService.Verify(service => service.GetCityNames(), Times.Never);
        VerifyNoOtherCalls();
    }

    // Purpose: unrecognized cities resolve the list but never touch the cache or upstream
    [Fact]
    public async Task GetCityGeocodingAsync_UnrecognizedCity_ReturnsCityNotFoundWithoutCacheOrUpstream()
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns(["London", "New York"]);

        // act
        var actual = await _target.GetCityGeocodingAsync("Atlantis");

        // assert
        Assert.Equal(CityGeocodingStatus.CityNotFound, actual.Status);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    // Purpose: mixed case and surrounding whitespace resolve to the canonical spelling and key
    [Theory]
    [InlineData("new york")]
    [InlineData("NEW YORK")]
    [InlineData("  New York  ")]
    public async Task GetCityGeocodingAsync_MixedCaseOrPaddedInput_ResolvesToCanonicalSpelling(string input)
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns(["London", Canonical]);
        _cacheRepository
            .Setup(repository => repository.GetAsync(NormalizedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync(
                Canonical, null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location(Canonical, "United States", 40.7128, -74.006, 8_804_190)));
        _cacheRepository
            .Setup(repository => repository.UpsertAsync(
                It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetCityGeocodingAsync(input);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Equal(Canonical, actual.Record!.DisplayName);
        Assert.Equal(NormalizedKey, actual.Record.NormalizedCityName);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(NormalizedKey, It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(Canonical, null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _timeProvider.Verify(clock => clock.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoOtherCalls();
    }

    // Purpose: a cache hit is returned without contacting the upstream service or clock
    [Fact]
    public async Task GetCityGeocodingAsync_CacheHit_ReturnsCachedRecordWithoutUpstream()
    {
        // arrange
        var cancellationToken = new CancellationTokenSource().Token;
        var cached = CachedRecord(population: 8_804_190);
        _cityService.Setup(service => service.GetCityNames()).Returns([Canonical]);
        _cacheRepository
            .Setup(repository => repository.GetAsync(NormalizedKey, cancellationToken))
            .ReturnsAsync(cached);

        // act
        var actual = await _target.GetCityGeocodingAsync(Canonical, cancellationToken);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Same(cached, actual.Record);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(NormalizedKey, cancellationToken), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
                It.IsAny<Format?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    // Purpose: a cache miss calls upstream once and persists the selected, time-stamped record
    [Fact]
    public async Task GetCityGeocodingAsync_CacheMiss_CallsUpstreamOnceAndPersistsSelectedResult()
    {
        // arrange
        var cancellationToken = new CancellationTokenSource().Token;
        _cityService.Setup(service => service.GetCityNames()).Returns([Canonical]);
        _cacheRepository
            .Setup(repository => repository.GetAsync(NormalizedKey, cancellationToken))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync(Canonical, null, "en", null, cancellationToken))
            .ReturnsAsync(ResponseWith(
                Location("Newark", "United States", 1, 1, 1),
                Location(Canonical, "United States", 40.7128, -74.006, 8_804_190)));
        _cacheRepository
            .Setup(repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), cancellationToken))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetCityGeocodingAsync(Canonical, cancellationToken);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Equal(40.7128, actual.Record!.Latitude);
        Assert.Equal(8_804_190, actual.Record.Population);
        Assert.Equal(RetrievedAtUtc, actual.Record.RetrievedAtUtc);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(NormalizedKey, cancellationToken), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(Canonical, null, "en", null, cancellationToken), Times.Once);
        _timeProvider.Verify(clock => clock.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.Is<GeocodingCacheRecord>(record =>
                    record.NormalizedCityName == NormalizedKey &&
                    record.DisplayName == Canonical &&
                    record.Country == "United States" &&
                    record.Latitude == 40.7128 &&
                    record.Longitude == -74.006 &&
                    record.Population == 8_804_190 &&
                    record.RetrievedAtUtc == RetrievedAtUtc),
                cancellationToken),
            Times.Once);
        VerifyNoOtherCalls();
    }

    // Purpose: a result without a population still succeeds and persists a null population
    [Fact]
    public async Task GetCityGeocodingAsync_UpstreamResultHasNoPopulation_SucceedsWithNullPopulation()
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns([Canonical]);
        _cacheRepository
            .Setup(repository => repository.GetAsync(NormalizedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync(Canonical, null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location(Canonical, "United States", 40.7128, -74.006, population: null)));
        _cacheRepository
            .Setup(repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // act
        var actual = await _target.GetCityGeocodingAsync(Canonical);

        // assert
        Assert.Equal(CityGeocodingStatus.Success, actual.Status);
        Assert.Null(actual.Record!.Population);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync(NormalizedKey, It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync(Canonical, null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _timeProvider.Verify(clock => clock.GetUtcNow(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(
                It.Is<GeocodingCacheRecord>(record => record.Population == null), It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyNoOtherCalls();
    }

    // Purpose: no exact name match yields a not-found outcome and persists nothing
    [Fact]
    public async Task GetCityGeocodingAsync_NoExactNameMatch_ReturnsGeocodingNotFoundWithoutPersisting()
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns(["Springfield"]);
        _cacheRepository
            .Setup(repository => repository.GetAsync("SPRINGFIELD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync("Springfield", null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseWith(Location("Springfields", "United States", 1, 1, 1)));

        // act
        var actual = await _target.GetCityGeocodingAsync("Springfield");

        // assert
        Assert.Equal(CityGeocodingStatus.GeocodingNotFound, actual.Status);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync("SPRINGFIELD", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync("Springfield", null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    // Purpose: empty upstream results yield a not-found outcome
    [Fact]
    public async Task GetCityGeocodingAsync_EmptyUpstreamResults_ReturnsGeocodingNotFound()
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns(["Nowhere"]);
        _cacheRepository
            .Setup(repository => repository.GetAsync("NOWHERE", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync("Nowhere", null, "en", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodingResponse { Results = new List<LocationResult>() });

        // act
        var actual = await _target.GetCityGeocodingAsync("Nowhere");

        // assert
        Assert.Equal(CityGeocodingStatus.GeocodingNotFound, actual.Status);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync("NOWHERE", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync("Nowhere", null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    // Purpose: an upstream API error on a cache miss reports service-unavailable and logs a warning
    [Fact]
    public async Task GetCityGeocodingAsync_UpstreamApiException_ReturnsServiceUnavailableAndLogsWarning()
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns(["Paris"]);
        _cacheRepository
            .Setup(repository => repository.GetAsync("PARIS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("upstream down", 503, "body", null, null));

        // act
        var actual = await _target.GetCityGeocodingAsync("Paris");

        // assert
        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, actual.Status);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync("PARIS", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyWarningLogged();
        VerifyNoOtherCalls();
    }

    // Purpose: an upstream transport failure reports service-unavailable and logs a warning
    [Fact]
    public async Task GetCityGeocodingAsync_UpstreamTransportFailure_ReturnsServiceUnavailableAndLogsWarning()
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns(["Paris"]);
        _cacheRepository
            .Setup(repository => repository.GetAsync("PARIS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        // act
        var actual = await _target.GetCityGeocodingAsync("Paris");

        // assert
        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, actual.Status);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync("PARIS", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyWarningLogged();
        VerifyNoOtherCalls();
    }

    // Purpose: an upstream timeout reports service-unavailable and logs a warning
    [Fact]
    public async Task GetCityGeocodingAsync_UpstreamTimeout_ReturnsServiceUnavailableAndLogsWarning()
    {
        // arrange
        _cityService.Setup(service => service.GetCityNames()).Returns(["Paris"]);
        _cacheRepository
            .Setup(repository => repository.GetAsync("PARIS", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("timeout"));

        // act
        var actual = await _target.GetCityGeocodingAsync("Paris", CancellationToken.None);

        // assert
        Assert.Equal(CityGeocodingStatus.ServiceUnavailable, actual.Status);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync("PARIS", It.IsAny<CancellationToken>()), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync("Paris", null, "en", null, It.IsAny<CancellationToken>()),
            Times.Once);
        VerifyWarningLogged();
        VerifyNoOtherCalls();
    }

    // Purpose: client cancellation propagates and is never treated as an upstream failure
    [Fact]
    public async Task GetCityGeocodingAsync_ClientCancellation_PropagatesOperationCanceledException()
    {
        // arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _cityService.Setup(service => service.GetCityNames()).Returns(["Paris"]);
        _cacheRepository
            .Setup(repository => repository.GetAsync("PARIS", cancellation.Token))
            .ReturnsAsync((GeocodingCacheRecord?)null);
        _openMeteoClient
            .Setup(client => client.SearchLocationsAsync("Paris", null, "en", null, cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        // act
        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _target.GetCityGeocodingAsync("Paris", cancellation.Token));

        // assert
        Assert.NotNull(actual);
        _cityService.Verify(service => service.GetCityNames(), Times.Once);
        _cacheRepository.Verify(
            repository => repository.GetAsync("PARIS", cancellation.Token), Times.Once);
        _openMeteoClient.Verify(
            client => client.SearchLocationsAsync("Paris", null, "en", null, cancellation.Token), Times.Once);
        _cacheRepository.Verify(
            repository => repository.UpsertAsync(It.IsAny<GeocodingCacheRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifyNoOtherCalls();
    }

    private void VerifyNoOtherCalls()
    {
        _cityService.VerifyNoOtherCalls();
        _openMeteoClient.VerifyNoOtherCalls();
        _cacheRepository.VerifyNoOtherCalls();
        _timeProvider.VerifyNoOtherCalls();
        _logger.VerifyNoOtherCalls();
    }

    private void VerifyWarningLogged() =>
        _logger.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Once);

    private static GeocodingResponse ResponseWith(params LocationResult[] results) =>
        new() { Results = results.ToList() };

    private static LocationResult Location(
        string name, string country, double latitude, double longitude, int? population) =>
        new()
        {
            Name = name,
            Country = country,
            Latitude = latitude,
            Longitude = longitude,
            Population = population,
        };

    private static GeocodingCacheRecord CachedRecord(long? population) =>
        new()
        {
            NormalizedCityName = NormalizedKey,
            DisplayName = Canonical,
            Country = "United States",
            Latitude = 40.7128,
            Longitude = -74.006,
            Population = population,
            RetrievedAtUtc = RetrievedAtUtc,
        };
}
