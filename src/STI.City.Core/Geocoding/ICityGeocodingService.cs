namespace STI.City.Core.Geocoding;

public interface ICityGeocodingService
{
    Task<CityGeocodingResult> GetAsync(
        string cityName,
        CancellationToken cancellationToken = default);
}
