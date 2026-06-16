using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Data.DependencyInjection;
using STI.City.Data.Repositories;
using STI.City.Data.Schema;

namespace STI.City.Tests.Cache;

/// <summary>
/// Stage 2 exit criteria, verified against an isolated SQLite database per test:
/// idempotent schema, key lookup, atomic unique upsert, full round-trip
/// (including nullable population and UTC timestamp), and cache-miss versus
/// SQLite-failure behavior.
/// </summary>
public sealed class GeocodingCacheRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"city-cache-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_dbPath};Pooling=False";

    public Task InitializeAsync() =>
        GeocodingCacheSchemaInitializer.InitializeAsync(ConnectionString);

    public Task DisposeAsync()
    {
        TryDeleteDatabase(_dbPath);
        return Task.CompletedTask;
    }

    private static void TryDeleteDatabase(string path)
    {
        // Release any pooled SQLite handles so the file is no longer locked.
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of an isolated temp database.
        }
    }

    [Fact]
    public async Task SchemaInitialization_IsIdempotent()
    {
        // InitializeAsync already ran once in InitializeAsync(); running again
        // must not throw.
        await GeocodingCacheSchemaInitializer.InitializeAsync(ConnectionString);
        await GeocodingCacheSchemaInitializer.InitializeAsync(ConnectionString);

        Assert.Equal(0, await CountRowsAsync());
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_OnCacheMiss()
    {
        var repository = CreateRepository();

        var result = await repository.GetAsync("DOES NOT EXIST");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertThenGet_RoundTripsAllFields()
    {
        var repository = CreateRepository();
        var record = new GeocodingCacheRecord
        {
            NormalizedCityName = "NEW YORK",
            DisplayName = "New York",
            Country = "United States",
            Latitude = 40.7128,
            Longitude = -74.006,
            Population = 8_804_190,
            RetrievedAtUtc = new DateTimeOffset(2026, 6, 16, 12, 30, 45, TimeSpan.Zero),
        };

        await repository.UpsertAsync(record);
        var fetched = await repository.GetAsync("NEW YORK");

        Assert.NotNull(fetched);
        Assert.Equal(record.NormalizedCityName, fetched!.NormalizedCityName);
        Assert.Equal(record.DisplayName, fetched.DisplayName);
        Assert.Equal(record.Country, fetched.Country);
        Assert.Equal(record.Latitude, fetched.Latitude);
        Assert.Equal(record.Longitude, fetched.Longitude);
        Assert.Equal(record.Population, fetched.Population);
        Assert.Equal(record.RetrievedAtUtc, fetched.RetrievedAtUtc);
    }

    [Fact]
    public async Task UpsertThenGet_RoundTripsNullPopulation()
    {
        var repository = CreateRepository();
        var record = new GeocodingCacheRecord
        {
            NormalizedCityName = "ATLANTIS",
            DisplayName = "Atlantis",
            Country = "Nowhere",
            Latitude = 0.0,
            Longitude = 0.0,
            Population = null,
            RetrievedAtUtc = DateTimeOffset.UtcNow,
        };

        await repository.UpsertAsync(record);
        var fetched = await repository.GetAsync("ATLANTIS");

        Assert.NotNull(fetched);
        Assert.Null(fetched!.Population);
    }

    [Fact]
    public async Task Upsert_IsAtomicAndLeavesExactlyOneRow_PerNormalizedName()
    {
        var repository = CreateRepository();
        var first = new GeocodingCacheRecord
        {
            NormalizedCityName = "LONDON",
            DisplayName = "London",
            Country = "United Kingdom",
            Latitude = 51.5074,
            Longitude = -0.1278,
            Population = 8_900_000,
            RetrievedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var second = first with
        {
            DisplayName = "London",
            Population = 9_000_000,
            RetrievedAtUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
        };

        await repository.UpsertAsync(first);
        await repository.UpsertAsync(second);

        Assert.Equal(1, await CountRowsAsync());
        var fetched = await repository.GetAsync("LONDON");
        Assert.NotNull(fetched);
        Assert.Equal(9_000_000, fetched!.Population);
        Assert.Equal(second.RetrievedAtUtc, fetched.RetrievedAtUtc);
    }

    [Fact]
    public async Task GetAsync_Throws_WhenSqliteFails_AndDoesNotLookLikeAMiss()
    {
        // A separate database whose schema was never created: a missing table is
        // an internal failure, distinct from a cache miss (which returns null).
        var brokenDbPath = Path.Combine(Path.GetTempPath(), $"city-broken-{Guid.NewGuid():N}.db");
        var repository = new SimpleRepoGeocodingCacheRepository(
            BuildConfiguration($"Data Source={brokenDbPath};Pooling=False"));

        try
        {
            await Assert.ThrowsAsync<SqliteException>(() => repository.GetAsync("ANYTHING"));
        }
        finally
        {
            TryDeleteDatabase(brokenDbPath);
        }
    }

    [Fact]
    public async Task GetAsync_Throws_WhenCancellationRequested()
    {
        var repository = CreateRepository();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetAsync("NEW YORK", cts.Token));
    }

    [Fact]
    public void AddCityData_RegistersRepository_AsTransient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(BuildConfiguration(ConnectionString));
        services.AddCityData();

        var descriptor = Assert.Single(
            services, d => d.ServiceType == typeof(IGeocodingCacheRepository));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IGeocodingCacheRepository>();
        var second = provider.GetRequiredService<IGeocodingCacheRepository>();
        Assert.NotSame(first, second);
    }

    private SimpleRepoGeocodingCacheRepository CreateRepository() =>
        new(BuildConfiguration(ConnectionString));

    private static IConfiguration BuildConfiguration(string connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CityCache"] = connectionString,
            })
            .Build();

    private async Task<long> CountRowsAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM GeocodingCache;";
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
