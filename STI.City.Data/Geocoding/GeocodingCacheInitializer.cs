using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace STI.City.Data.Geocoding;

internal sealed class GeocodingCacheInitializer(
    IConfiguration configuration) : IGeocodingCacheInitializer
{
    private const string CreateTableSql =
        """
        CREATE TABLE IF NOT EXISTS GeocodingCache
        (
            NormalizedCityName TEXT NOT NULL PRIMARY KEY,
            DisplayName TEXT NOT NULL,
            Country TEXT NOT NULL,
            Latitude REAL NOT NULL CHECK (Latitude BETWEEN -90 AND 90),
            Longitude REAL NOT NULL CHECK (Longitude BETWEEN -180 AND 180),
            Population INTEGER NULL CHECK (Population IS NULL OR Population >= 0),
            RetrievedAtUtc TEXT NOT NULL
        );
        """;

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(
            configuration.GetConnectionString("CityCache"));

        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                CreateTableSql,
                cancellationToken: cancellationToken));
    }
}
