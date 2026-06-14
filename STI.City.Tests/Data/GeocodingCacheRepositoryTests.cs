using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Geocoding;
using STI.City.Data;

namespace STI.City.Tests.Data;

public sealed class GeocodingCacheRepositoryTests
{
    [Fact]
    public async Task SchemaInitializationIsIdempotent()
    {
        await using var context = CreateContext();

        var initializer = context.Services.GetRequiredService<
            IGeocodingCacheInitializer>();

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        await using var connection =
            new SqliteConnection(context.ConnectionString);
        var tableCount = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'GeocodingCache';
            """);

        Assert.Equal(1, tableCount);
    }

    [Fact]
    public void RepositoryRegistrationIsTransient()
    {
        using var context = CreateContext();

        var first = context.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var second = context.Services.GetRequiredService<
            IGeocodingCacheRepository>();

        Assert.NotSame(first, second);
    }

    [Theory]
    [InlineData(8_804_190L)]
    [InlineData(null)]
    public async Task AllFieldsRoundTrip(long? population)
    {
        await using var context = CreateContext();
        await context.InitializeAsync();
        var repository = context.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var expected = CreateRecord(population);

        await repository.UpsertAsync(expected);
        var actual = await repository.GetAsync(
            expected.NormalizedCityName);

        Assert.Equal(expected, actual);
        Assert.Equal(TimeSpan.Zero, actual!.RetrievedAtUtc.Offset);
    }

    [Fact]
    public async Task UpsertUpdatesOneRowAtomically()
    {
        await using var context = CreateContext();
        await context.InitializeAsync();
        var repository = context.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var original = CreateRecord(8_804_190);
        var updated = original with
        {
            Country = "USA",
            Population = 8_900_000,
            RetrievedAtUtc = original.RetrievedAtUtc.AddHours(1)
        };

        await repository.UpsertAsync(original);
        await repository.UpsertAsync(updated);

        Assert.Equal(
            updated,
            await repository.GetAsync(updated.NormalizedCityName));

        await using var connection =
            new SqliteConnection(context.ConnectionString);
        var rowCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM GeocodingCache;");
        Assert.Equal(1, rowCount);
    }

    [Theory]
    [InlineData(-91, 0, 1)]
    [InlineData(91, 0, 1)]
    [InlineData(0, -181, 1)]
    [InlineData(0, 181, 1)]
    [InlineData(0, 0, -1)]
    public async Task DatabaseConstraintsRejectInvalidValues(
        double latitude,
        double longitude,
        long population)
    {
        await using var context = CreateContext();
        await context.InitializeAsync();
        var repository = context.Services.GetRequiredService<
            IGeocodingCacheRepository>();
        var record = CreateRecord(population) with
        {
            Latitude = latitude,
            Longitude = longitude
        };

        await Assert.ThrowsAsync<SqliteException>(
            () => repository.UpsertAsync(record));
    }

    [Fact]
    public async Task CacheMissReturnsNull()
    {
        await using var context = CreateContext();
        await context.InitializeAsync();
        var repository = context.Services.GetRequiredService<
            IGeocodingCacheRepository>();

        var result = await repository.GetAsync("MISSING");

        Assert.Null(result);
    }

    [Fact]
    public async Task SqliteFailureIsNotReportedAsCacheMiss()
    {
        var connectionString =
            $"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "cache.db")}";
        await using var context = CreateContext(connectionString);
        var repository = context.Services.GetRequiredService<
            IGeocodingCacheRepository>();

        await Assert.ThrowsAsync<SqliteException>(
            () => repository.GetAsync("NEW YORK"));
    }

    private static GeocodingCacheRecord CreateRecord(long? population) =>
        new(
            "NEW YORK",
            "New York",
            "United States",
            40.7128,
            -74.0060,
            population,
            new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero));

    private static SqliteTestContext CreateContext(
        string? connectionString = null) =>
        new(connectionString);

    private sealed class SqliteTestContext : IAsyncDisposable, IDisposable
    {
        private readonly string _databasePath;

        public SqliteTestContext(string? connectionString)
        {
            _databasePath = Path.Combine(
                Path.GetTempPath(),
                $"city-cache-{Guid.NewGuid():N}.db");
            ConnectionString =
                connectionString ?? $"Data Source={_databasePath}";

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:CityCache"] = ConnectionString
                    })
                .Build();

            Services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddCityData()
                .BuildServiceProvider();
        }

        public string ConnectionString { get; }

        public ServiceProvider Services { get; }

        public Task InitializeAsync() =>
            Services.GetRequiredService<IGeocodingCacheInitializer>()
                .InitializeAsync();

        public void Dispose()
        {
            Services.Dispose();
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }

        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }
    }
}
