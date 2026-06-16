using Microsoft.Data.Sqlite;

namespace STI.City.Data.Schema;

/// <summary>
/// Creates the <c>GeocodingCache</c> table and its constraints. Idempotent, so
/// it can run on every startup before the application accepts requests.
/// </summary>
public static class GeocodingCacheSchemaInitializer
{
    public static async Task InitializeAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS GeocodingCache (
                NormalizedCityName TEXT    NOT NULL PRIMARY KEY,
                DisplayName        TEXT    NOT NULL,
                Country            TEXT    NOT NULL,
                Latitude           REAL    NOT NULL CHECK (Latitude  BETWEEN -90  AND 90),
                Longitude          REAL    NOT NULL CHECK (Longitude BETWEEN -180 AND 180),
                Population         INTEGER NULL     CHECK (Population IS NULL OR Population >= 0),
                RetrievedAtUtc     TEXT    NOT NULL
            );
            """;

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
