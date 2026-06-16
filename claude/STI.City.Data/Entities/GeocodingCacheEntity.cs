using Dapper;
using Formula.SimpleRepo;
using Microsoft.Data.Sqlite;

namespace STI.City.Data.Entities;

/// <summary>
/// Formula.SimpleRepo persistence model mapped to the <c>GeocodingCache</c>
/// SQLite table. The <see cref="ConnectionDetails"/> attribute binds the model
/// to the <c>CityCache</c> connection string and the SQLite dialect.
/// </summary>
[ConnectionDetails("CityCache", typeof(SqliteConnection), Dapper.SimpleCRUD.Dialect.SQLite)]
[Table("GeocodingCache")]
public class GeocodingCacheEntity
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
