using System.Globalization;
using Dapper;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using STI.City.Core.Models;
using STI.City.Core.Repositories;
using STI.City.Data.Entities;

namespace STI.City.Data.Repositories;

/// <summary>
/// SQLite-backed cache repository built on Formula.SimpleRepo. Reads use the
/// verified SimpleRepo key lookup; the atomic insert/upsert runs as a single
/// SQLite <c>ON CONFLICT</c> statement through Dapper.
/// </summary>
[Repo]
public class SimpleRepoGeocodingCacheRepository
    : RepositoryBase<GeocodingCacheEntity, GeocodingCacheEntity>, IGeocodingCacheRepository
{
    private readonly string _connectionString;

    public SimpleRepoGeocodingCacheRepository(IConfiguration configuration)
        : base(configuration)
    {
        _connectionString = configuration.GetConnectionString("CityCache")
            ?? throw new InvalidOperationException(
                "Configuration value 'ConnectionStrings:CityCache' is required and must not be blank.");
    }

    public async Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Cast to object so SimpleRepo binds the primary-key lookup overload
        // rather than the JSON-constraint string overload.
        GeocodingCacheEntity? entity = await GetAsync((object)normalizedCityName);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO GeocodingCache
                (NormalizedCityName, DisplayName, Country, Latitude, Longitude, Population, RetrievedAtUtc)
            VALUES
                (@NormalizedCityName, @DisplayName, @Country, @Latitude, @Longitude, @Population, @RetrievedAtUtc)
            ON CONFLICT(NormalizedCityName) DO UPDATE SET
                DisplayName    = excluded.DisplayName,
                Country        = excluded.Country,
                Latitude       = excluded.Latitude,
                Longitude      = excluded.Longitude,
                Population     = excluded.Population,
                RetrievedAtUtc = excluded.RetrievedAtUtc;
            """;

        var parameters = new
        {
            record.NormalizedCityName,
            record.DisplayName,
            record.Country,
            record.Latitude,
            record.Longitude,
            record.Population,
            RetrievedAtUtc = ToStorage(record.RetrievedAtUtc),
        };

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static GeocodingCacheRecord ToRecord(GeocodingCacheEntity entity) => new()
    {
        NormalizedCityName = entity.NormalizedCityName,
        DisplayName = entity.DisplayName,
        Country = entity.Country,
        Latitude = entity.Latitude,
        Longitude = entity.Longitude,
        Population = entity.Population,
        RetrievedAtUtc = FromStorage(entity.RetrievedAtUtc),
    };

    private static string ToStorage(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset FromStorage(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal);
}
