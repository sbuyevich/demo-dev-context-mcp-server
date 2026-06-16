namespace STI.City.API.Configuration;

/// <summary>
/// Validates the SQLite cache configuration so startup fails fast when it is
/// missing or blank.
/// </summary>
public static class CacheConfiguration
{
    /// <summary>Name of the cache connection string under <c>ConnectionStrings</c>.</summary>
    public const string CacheConnectionStringName = "CityCache";

    /// <summary>
    /// Returns the configured cache connection string, throwing when it is
    /// absent or blank.
    /// </summary>
    public static string GetRequiredCacheConnectionString(this IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(CacheConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Configuration value 'ConnectionStrings:{CacheConnectionStringName}' is required and must not be blank.");
        }

        return connectionString;
    }
}
