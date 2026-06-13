namespace STI.City.Core.Geocoding;

public interface IGeocodingCacheRepository
{
    Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default);
}
