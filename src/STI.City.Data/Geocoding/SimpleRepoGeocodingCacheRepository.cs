using System.Globalization;
using Dapper;
using Formula.SimpleRepo;
using Microsoft.Extensions.Configuration;
using STI.City.Core.Geocoding;

namespace STI.City.Data.Geocoding;

[Repo]
public sealed class SimpleRepoGeocodingCacheRepository(
    IConfiguration configuration)
    : RepositoryBase<GeocodingCacheEntity, GeocodingCacheEntity>(configuration),
      IGeocodingCacheRepository,
      ICityCacheSchemaInitializer,
      IDisposable
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS GeocodingCache (
            NormalizedCityName TEXT NOT NULL PRIMARY KEY,
            DisplayName TEXT NOT NULL,
            Country TEXT NOT NULL,
            Latitude REAL NOT NULL
                CHECK (Latitude >= -90 AND Latitude <= 90),
            Longitude REAL NOT NULL
                CHECK (Longitude >= -180 AND Longitude <= 180),
            Population INTEGER NULL
                CHECK (Population IS NULL OR Population >= 0),
            RetrievedAtUtc TEXT NOT NULL
        );
        """;

    private const string UpsertSql = """
        INSERT INTO GeocodingCache (
            NormalizedCityName,
            DisplayName,
            Country,
            Latitude,
            Longitude,
            Population,
            RetrievedAtUtc)
        VALUES (
            @NormalizedCityName,
            @DisplayName,
            @Country,
            @Latitude,
            @Longitude,
            @Population,
            @RetrievedAtUtc)
        ON CONFLICT(NormalizedCityName) DO UPDATE SET
            DisplayName = excluded.DisplayName,
            Country = excluded.Country,
            Latitude = excluded.Latitude,
            Longitude = excluded.Longitude,
            Population = excluded.Population,
            RetrievedAtUtc = excluded.RetrievedAtUtc;
        """;

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _connection.ExecuteAsync(
            new CommandDefinition(
                CreateTableSql,
                cancellationToken: cancellationToken));

    public async Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedCityName);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await base.GetAsync(
            (object)normalizedCityName,
            transaction: null,
            commandTimeout: null);

        cancellationToken.ThrowIfCancellationRequested();
        return entity is null ? null : ToRecord(entity);
    }

    public Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        return _connection.ExecuteAsync(
            new CommandDefinition(
                UpsertSql,
                ToEntity(record),
                cancellationToken: cancellationToken));
    }

    public void Dispose() => _connection.Dispose();

    private static GeocodingCacheEntity ToEntity(GeocodingCacheRecord record) =>
        new()
        {
            NormalizedCityName = record.NormalizedCityName,
            DisplayName = record.DisplayName,
            Country = record.Country,
            Latitude = record.Latitude,
            Longitude = record.Longitude,
            Population = record.Population,
            RetrievedAtUtc = record.RetrievedAtUtc
                .ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture)
        };

    private static GeocodingCacheRecord ToRecord(GeocodingCacheEntity entity) =>
        new(
            entity.NormalizedCityName,
            entity.DisplayName,
            entity.Country,
            entity.Latitude,
            entity.Longitude,
            entity.Population,
            DateTimeOffset.Parse(
                entity.RetrievedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind));
}
