using System.Collections;
using System.Globalization;
using Dapper;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using STI.City.Core.Geocoding;

namespace STI.City.Data.Geocoding;

[Repo]
public sealed class SimpleRepoGeocodingCacheRepository
    : RepositoryBase<GeocodingCacheEntity, GeocodingCacheEntity>,
        IGeocodingCacheRepository
{
    private readonly IConfiguration _configuration;

    private const string UpsertSql =
        """
        INSERT INTO GeocodingCache
        (
            NormalizedCityName,
            DisplayName,
            Country,
            Latitude,
            Longitude,
            Population,
            RetrievedAtUtc
        )
        VALUES
        (
            @NormalizedCityName,
            @DisplayName,
            @Country,
            @Latitude,
            @Longitude,
            @Population,
            @RetrievedAtUtc
        )
        ON CONFLICT(NormalizedCityName) DO UPDATE SET
            DisplayName = excluded.DisplayName,
            Country = excluded.Country,
            Latitude = excluded.Latitude,
            Longitude = excluded.Longitude,
            Population = excluded.Population,
            RetrievedAtUtc = excluded.RetrievedAtUtc;
        """;

    public SimpleRepoGeocodingCacheRepository(IConfiguration configuration)
        : base(configuration)
    {
        _configuration = configuration;
    }

    public async Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedCityName);
        cancellationToken.ThrowIfCancellationRequested();

        var entities = await base.GetAsync(
            new Hashtable
            {
                [nameof(GeocodingCacheEntity.NormalizedCityName)] =
                    normalizedCityName
            });

        var entity = entities.SingleOrDefault();
        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = ToEntity(record);
        await using var connection = new SqliteConnection(
            _configuration.GetConnectionString("CityCache"));

        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                UpsertSql,
                entity,
                cancellationToken: cancellationToken));
    }

    private static GeocodingCacheRecord ToRecord(
        GeocodingCacheEntity entity) =>
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

    private static GeocodingCacheEntity ToEntity(
        GeocodingCacheRecord record) =>
        new()
        {
            NormalizedCityName = record.NormalizedCityName,
            DisplayName = record.DisplayName,
            Country = record.Country,
            Latitude = record.Latitude,
            Longitude = record.Longitude,
            Population = record.Population,
            RetrievedAtUtc = record.RetrievedAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture)
        };
}
