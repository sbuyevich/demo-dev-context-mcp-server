using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Geocoding;
using STI.City.Data;
using STI.City.Data.Geocoding;

namespace STI.City.Tests.Geocoding;

public sealed class SimpleRepoGeocodingCacheRepositoryTests
{
    [Fact]
    public async Task Repository_contract_uses_transient_lifetime()
    {
        await using var database = new TestDatabase();

        var first = database.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var second = database.Services.GetRequiredService<
            IGeocodingCacheRepository>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Schema_initialization_is_idempotent()
    {
        await using var database = new TestDatabase();

        var initializer = database.Services.GetRequiredService<
            ICityCacheSchemaInitializer>();

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        await using var connection = database.CreateConnection();
        var tableCount = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'GeocodingCache';
            """);

        Assert.Equal(1, tableCount);
    }

    [Fact]
    public async Task Upsert_and_lookup_round_trip_all_fields()
    {
        await using var database = new TestDatabase();
        await database.InitializeAsync();
        var repository = database.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var expected = new GeocodingCacheRecord(
            "CHICAGO",
            "Chicago",
            "United States",
            41.85003,
            -87.65005,
            null,
            new DateTimeOffset(2026, 6, 12, 18, 30, 0, TimeSpan.Zero));

        await repository.UpsertAsync(expected);
        var actual = await repository.GetAsync(expected.NormalizedCityName);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Upsert_updates_existing_row_without_creating_a_duplicate()
    {
        await using var database = new TestDatabase();
        await database.InitializeAsync();
        var repository = database.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var original = CreateRecord(population: 2_600_000);
        var updated = original with
        {
            Country = "USA",
            Population = 2_700_000,
            RetrievedAtUtc = original.RetrievedAtUtc.AddHours(1)
        };

        await repository.UpsertAsync(original);
        await repository.UpsertAsync(updated);

        var actual = await repository.GetAsync(updated.NormalizedCityName);
        await using var connection = database.CreateConnection();
        var rowCount = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM GeocodingCache
            WHERE NormalizedCityName = @NormalizedCityName;
            """,
            new { updated.NormalizedCityName });

        Assert.Equal(updated, actual);
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task Missing_record_returns_null_but_database_failure_throws()
    {
        await using var database = new TestDatabase();
        await database.InitializeAsync();
        var repository = database.Services.GetRequiredService<
            IGeocodingCacheRepository>();

        var missing = await repository.GetAsync("MISSING");

        await using (var connection = database.CreateConnection())
        {
            await connection.ExecuteAsync("DROP TABLE GeocodingCache;");
        }

        await Assert.ThrowsAsync<SqliteException>(
            () => repository.GetAsync("MISSING"));
        Assert.Null(missing);
    }

    [Fact]
    public async Task Database_constraints_reject_invalid_values()
    {
        await using var database = new TestDatabase();
        await database.InitializeAsync();
        var repository = database.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var invalid = CreateRecord(population: -1) with
        {
            Latitude = 91,
            Longitude = -181
        };

        await Assert.ThrowsAsync<SqliteException>(
            () => repository.UpsertAsync(invalid));
    }

    private static GeocodingCacheRecord CreateRecord(long? population) =>
        new(
            "CHICAGO",
            "Chicago",
            "United States",
            41.85003,
            -87.65005,
            population,
            new DateTimeOffset(2026, 6, 12, 18, 30, 0, TimeSpan.Zero));

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _databasePath =
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

        public TestDatabase()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:CityCache"] =
                            $"Data Source={_databasePath};Pooling=False"
                    })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddCityData();
            Services = services.BuildServiceProvider();
        }

        public ServiceProvider Services { get; }

        public SqliteConnection CreateConnection() =>
            new($"Data Source={_databasePath};Pooling=False");

        public Task InitializeAsync() =>
            Services
                .GetRequiredService<ICityCacheSchemaInitializer>()
                .InitializeAsync();

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            SqliteConnection.ClearAllPools();

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
