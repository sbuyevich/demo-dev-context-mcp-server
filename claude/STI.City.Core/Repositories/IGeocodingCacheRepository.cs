using STI.City.Core.Models;

namespace STI.City.Core.Repositories;

/// <summary>
/// Abstracts cache retrieval and atomic insert/upsert of geocoding records.
/// Implemented in the Data layer over SQLite; depended upon by Core services.
/// </summary>
public interface IGeocodingCacheRepository
{
    /// <summary>
    /// Returns the cached record for <paramref name="normalizedCityName"/>, or
    /// <c>null</c> on a cache miss. Storage failures propagate as exceptions and
    /// must remain distinguishable from a miss.
    /// </summary>
    Task<GeocodingCacheRecord?> GetAsync(
        string normalizedCityName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically inserts or updates the record so exactly one row exists per
    /// normalized city name.
    /// </summary>
    Task UpsertAsync(
        GeocodingCacheRecord record,
        CancellationToken cancellationToken = default);
}
