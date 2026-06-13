using Demo.Cities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenMeteo.Api.Client;
using STI.City.Core;
using STI.City.Core.Geocoding;

namespace STI.City.Tests.Geocoding;

public sealed class CityGeocodingServiceTests
{
    private static readonly DateTimeOffset RetrievedAtUtc =
        new(2026, 6, 13, 1, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Service_is_registered_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddCityCore();

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType ==
                typeof(ICityGeocodingService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(
            typeof(CityGeocodingService),
            descriptor.ImplementationType);
    }

    [Fact]
    public async Task Cache_hit_returns_cached_record_without_calling_upstream()
    {
        var cachedRecord = CreateRecord(population: 2_700_000);
        var repository = new FakeCacheRepository
        {
            RecordToReturn = cachedRecord
        };
        var client = new FakeOpenMeteoClient();
        var service = CreateService(repository, client);

        var result = await service.GetAsync("  cHiCaGo  ");

        Assert.Equal(CityGeocodingOutcome.Success, result.Outcome);
        Assert.Equal(cachedRecord, result.Record);
        Assert.Equal(["CHICAGO"], repository.RequestedKeys);
        Assert.Equal(0, client.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Atlantis")]
    public async Task Unknown_city_skips_cache_and_upstream(string cityName)
    {
        var repository = new FakeCacheRepository();
        var client = new FakeOpenMeteoClient();
        var service = CreateService(repository, client);

        var result = await service.GetAsync(cityName);

        Assert.Equal(CityGeocodingOutcome.CityNotFound, result.Outcome);
        Assert.Null(result.Record);
        Assert.Empty(repository.RequestedKeys);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task Cache_miss_selects_first_exact_match_and_persists_record()
    {
        var repository = new FakeCacheRepository();
        var client = new FakeOpenMeteoClient
        {
            Response = CreateResponse(
                CreateLocation("Chicago Heights", 41, -87, 27_000),
                CreateLocation("CHICAGO", 41.85003, -87.65005, 2_600_000),
                CreateLocation("Chicago", 42, -88, 2_700_000))
        };
        var service = CreateService(repository, client);
        using var cancellation = new CancellationTokenSource();

        var result = await service.GetAsync(
            "chicago",
            cancellation.Token);

        var expected = CreateRecord(population: 2_600_000);
        Assert.Equal(CityGeocodingOutcome.Success, result.Outcome);
        Assert.Equal(expected, result.Record);
        Assert.Equal(expected, repository.UpsertedRecord);
        Assert.Equal("Chicago", client.LastName);
        Assert.Null(client.LastCount);
        Assert.Equal("en", client.LastLanguage);
        Assert.Equal(Format.Json, client.LastFormat);
        Assert.Equal(cancellation.Token, client.LastCancellationToken);
        Assert.Equal(cancellation.Token, repository.LastGetCancellationToken);
        Assert.Equal(
            cancellation.Token,
            repository.LastUpsertCancellationToken);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task Missing_population_is_preserved_in_shared_result()
    {
        var repository = new FakeCacheRepository();
        var client = new FakeOpenMeteoClient
        {
            Response = CreateResponse(
                CreateLocation("Chicago", 41.85003, -87.65005, null))
        };
        var service = CreateService(repository, client);

        var result = await service.GetAsync("Chicago");

        Assert.Equal(CityGeocodingOutcome.Success, result.Outcome);
        Assert.Null(result.Record!.Population);
        Assert.Equal(result.Record, repository.UpsertedRecord);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_or_nonmatching_results_return_geocoding_not_found(
        bool includeNonmatchingResult)
    {
        var repository = new FakeCacheRepository();
        var client = new FakeOpenMeteoClient
        {
            Response = includeNonmatchingResult
                ? CreateResponse(
                    CreateLocation("Chicago Heights", 41, -87, 27_000))
                : CreateResponse()
        };
        var service = CreateService(repository, client);

        var result = await service.GetAsync("Chicago");

        Assert.Equal(
            CityGeocodingOutcome.GeocodingNotFound,
            result.Outcome);
        Assert.Null(result.Record);
        Assert.Null(repository.UpsertedRecord);
    }

    [Fact]
    public async Task Api_failure_returns_service_unavailable()
    {
        var client = new FakeOpenMeteoClient
        {
            ExceptionToThrow = new ApiException(
                "Unavailable",
                503,
                string.Empty,
                new Dictionary<string, IEnumerable<string>>(),
                new InvalidOperationException())
        };
        var service = CreateService(new FakeCacheRepository(), client);

        var result = await service.GetAsync("Chicago");

        Assert.Equal(
            CityGeocodingOutcome.ServiceUnavailable,
            result.Outcome);
    }

    [Fact]
    public async Task Transport_failure_returns_service_unavailable()
    {
        var client = new FakeOpenMeteoClient
        {
            ExceptionToThrow = new HttpRequestException("Network failed.")
        };
        var service = CreateService(new FakeCacheRepository(), client);

        var result = await service.GetAsync("Chicago");

        Assert.Equal(
            CityGeocodingOutcome.ServiceUnavailable,
            result.Outcome);
    }

    [Fact]
    public async Task Timeout_returns_service_unavailable()
    {
        var client = new FakeOpenMeteoClient
        {
            ExceptionToThrow = new TaskCanceledException("Timed out.")
        };
        var service = CreateService(new FakeCacheRepository(), client);

        var result = await service.GetAsync("Chicago");

        Assert.Equal(
            CityGeocodingOutcome.ServiceUnavailable,
            result.Outcome);
    }

    [Fact]
    public async Task Request_cancellation_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new FakeOpenMeteoClient
        {
            ExceptionToThrow = new OperationCanceledException(
                cancellation.Token)
        };
        var service = CreateService(new FakeCacheRepository(), client);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetAsync("Chicago", cancellation.Token));
    }

    [Fact]
    public async Task Persistence_failure_propagates()
    {
        var repository = new FakeCacheRepository
        {
            UpsertException = new InvalidOperationException(
                "Database unavailable.")
        };
        var client = new FakeOpenMeteoClient
        {
            Response = CreateResponse(
                CreateLocation("Chicago", 41.85003, -87.65005, 2_600_000))
        };
        var service = CreateService(repository, client);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAsync("Chicago"));
    }

    [Fact]
    public async Task Cache_lookup_failure_propagates()
    {
        var repository = new FakeCacheRepository
        {
            GetException = new InvalidOperationException(
                "Database unavailable.")
        };
        var client = new FakeOpenMeteoClient();
        var service = CreateService(repository, client);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAsync("Chicago"));
        Assert.Equal(0, client.CallCount);
    }

    private static CityGeocodingService CreateService(
        FakeCacheRepository repository,
        FakeOpenMeteoClient client) =>
        new(
            new FakeCityService(),
            client,
            repository,
            new FixedTimeProvider(RetrievedAtUtc),
            NullLogger<CityGeocodingService>.Instance);

    private static GeocodingCacheRecord CreateRecord(long? population) =>
        new(
            "CHICAGO",
            "Chicago",
            "United States",
            41.85003,
            -87.65005,
            population,
            RetrievedAtUtc);

    private static GeocodingResponse CreateResponse(
        params LocationResult[] results) =>
        new()
        {
            Results = results
        };

    private static LocationResult CreateLocation(
        string name,
        double latitude,
        double longitude,
        int? population) =>
        new()
        {
            Name = name,
            Country = "United States",
            Latitude = latitude,
            Longitude = longitude,
            Population = population
        };

    private sealed class FakeCityService : ICityService
    {
        public IReadOnlyList<string> GetCityNames() =>
            ["Chicago", "London", "Tokyo"];
    }

    private sealed class FakeCacheRepository : IGeocodingCacheRepository
    {
        public GeocodingCacheRecord? RecordToReturn { get; init; }

        public GeocodingCacheRecord? UpsertedRecord { get; private set; }

        public Exception? GetException { get; init; }

        public Exception? UpsertException { get; init; }

        public List<string> RequestedKeys { get; } = [];

        public CancellationToken LastGetCancellationToken { get; private set; }

        public CancellationToken LastUpsertCancellationToken
        {
            get;
            private set;
        }

        public Task<GeocodingCacheRecord?> GetAsync(
            string normalizedCityName,
            CancellationToken cancellationToken = default)
        {
            RequestedKeys.Add(normalizedCityName);
            LastGetCancellationToken = cancellationToken;

            if (GetException is not null)
            {
                return Task.FromException<GeocodingCacheRecord?>(
                    GetException);
            }

            return Task.FromResult(RecordToReturn);
        }

        public Task UpsertAsync(
            GeocodingCacheRecord record,
            CancellationToken cancellationToken = default)
        {
            LastUpsertCancellationToken = cancellationToken;

            if (UpsertException is not null)
            {
                return Task.FromException(UpsertException);
            }

            UpsertedRecord = record;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOpenMeteoClient : IOpenMeteoClient
    {
        public GeocodingResponse Response { get; init; } = CreateResponse();

        public Exception? ExceptionToThrow { get; init; }

        public int CallCount { get; private set; }

        public string? LastName { get; private set; }

        public int? LastCount { get; private set; }

        public string? LastLanguage { get; private set; }

        public Format? LastFormat { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<GeocodingResponse> SearchLocationsAsync(
            string name,
            int? count,
            string language,
            Format? format) =>
            SearchLocationsAsync(
                name,
                count,
                language,
                format,
                CancellationToken.None);

        public Task<GeocodingResponse> SearchLocationsAsync(
            string name,
            int? count,
            string language,
            Format? format,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastName = name;
            LastCount = count;
            LastLanguage = language;
            LastFormat = format;
            LastCancellationToken = cancellationToken;

            if (ExceptionToThrow is not null)
            {
                return Task.FromException<GeocodingResponse>(
                    ExceptionToThrow);
            }

            return Task.FromResult(Response);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
