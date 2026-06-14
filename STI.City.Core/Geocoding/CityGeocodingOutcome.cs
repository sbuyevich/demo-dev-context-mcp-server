namespace STI.City.Core.Geocoding;

public enum CityGeocodingOutcomeKind
{
    Success,
    CityNotFound,
    GeocodingNotFound,
    ServiceUnavailable
}

public sealed record CityGeocodingOutcome(
    CityGeocodingOutcomeKind Kind,
    GeocodingCacheRecord? Record = null)
{
    public static CityGeocodingOutcome Success(
        GeocodingCacheRecord record) =>
        new(CityGeocodingOutcomeKind.Success, record);

    public static CityGeocodingOutcome CityNotFound() =>
        new(CityGeocodingOutcomeKind.CityNotFound);

    public static CityGeocodingOutcome GeocodingNotFound() =>
        new(CityGeocodingOutcomeKind.GeocodingNotFound);

    public static CityGeocodingOutcome ServiceUnavailable() =>
        new(CityGeocodingOutcomeKind.ServiceUnavailable);
}
