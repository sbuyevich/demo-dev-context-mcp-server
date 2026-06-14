using Dapper;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;

namespace STI.City.Data.Geocoding;

[ConnectionDetails(
    "CityCache",
    typeof(SqliteConnection),
    SimpleCRUD.Dialect.SQLite)]
[Table("GeocodingCache")]
public sealed class GeocodingCacheEntity
{
    [Key]
    public string NormalizedCityName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public long? Population { get; set; }

    public string RetrievedAtUtc { get; set; } = string.Empty;
}
