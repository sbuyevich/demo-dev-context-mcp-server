namespace STI.City.Core.Geocoding;

public interface ICityGeocodingService
{
    Task<CityGeocodingOutcome> GetAsync(
        string cityName,
        CancellationToken cancellationToken = default);
}
