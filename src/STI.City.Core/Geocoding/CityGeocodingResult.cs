namespace STI.City.Core.Geocoding;

public sealed record CityGeocodingResult
{
    private CityGeocodingResult(
        CityGeocodingOutcome outcome,
        GeocodingCacheRecord? record)
    {
        Outcome = outcome;
        Record = record;
    }

    public CityGeocodingOutcome Outcome { get; }

    public GeocodingCacheRecord? Record { get; }

    public static CityGeocodingResult Success(
        GeocodingCacheRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(CityGeocodingOutcome.Success, record);
    }

    public static CityGeocodingResult CityNotFound() =>
        new(CityGeocodingOutcome.CityNotFound, null);

    public static CityGeocodingResult GeocodingNotFound() =>
        new(CityGeocodingOutcome.GeocodingNotFound, null);

    public static CityGeocodingResult ServiceUnavailable() =>
        new(CityGeocodingOutcome.ServiceUnavailable, null);
}
