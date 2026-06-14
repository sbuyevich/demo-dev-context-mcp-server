namespace STI.City.Core.Geocoding;

public sealed record GeocodingCacheRecord(
    string NormalizedCityName,
    string DisplayName,
    string Country,
    double Latitude,
    double Longitude,
    long? Population,
    DateTimeOffset RetrievedAtUtc);
