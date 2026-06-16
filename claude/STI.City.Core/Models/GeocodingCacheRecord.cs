namespace STI.City.Core.Models;

/// <summary>
/// Internal persistence/application representation of a cached geocoding result.
/// A single record supplies both the location and population detail endpoints.
/// </summary>
public sealed record GeocodingCacheRecord
{
    /// <summary>Primary cache key: <c>DisplayName.Trim().ToUpperInvariant()</c>.</summary>
    public required string NormalizedCityName { get; init; }

    /// <summary>Canonical, package-provided spelling of the city name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Country returned by Open-Meteo.</summary>
    public required string Country { get; init; }

    /// <summary>Latitude in the range -90 through 90.</summary>
    public required double Latitude { get; init; }

    /// <summary>Longitude in the range -180 through 180.</summary>
    public required double Longitude { get; init; }

    /// <summary>Population when known; <c>null</c> when Open-Meteo supplied no value.</summary>
    public long? Population { get; init; }

    /// <summary>UTC timestamp captured from the injected <see cref="System.TimeProvider"/>.</summary>
    public required DateTimeOffset RetrievedAtUtc { get; init; }
}
