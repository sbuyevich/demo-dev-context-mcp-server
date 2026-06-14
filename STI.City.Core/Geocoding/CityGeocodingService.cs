using Demo.Cities;
using Microsoft.Extensions.Logging;
using OpenMeteo.Api.Client;

namespace STI.City.Core.Geocoding;

public sealed class CityGeocodingService(
    ICityService cityService,
    IOpenMeteoClient openMeteoClient,
    IGeocodingCacheRepository cacheRepository,
    TimeProvider timeProvider,
    ILogger<CityGeocodingService> logger) : ICityGeocodingService
{
    public async Task<CityGeocodingOutcome> GetAsync(
        string cityName,
        CancellationToken cancellationToken = default)
    {
        var requestedName = cityName?.Trim();
        if (string.IsNullOrEmpty(requestedName))
        {
            return CityGeocodingOutcome.CityNotFound();
        }

        var canonicalName = cityService.GetCityNames()
            .FirstOrDefault(name =>
                StringComparer.OrdinalIgnoreCase.Equals(
                    name,
                    requestedName));

        if (canonicalName is null)
        {
            return CityGeocodingOutcome.CityNotFound();
        }

        var normalizedName = canonicalName.Trim().ToUpperInvariant();
        var cached = await cacheRepository.GetAsync(
            normalizedName,
            cancellationToken);

        if (cached is not null)
        {
            return CityGeocodingOutcome.Success(cached);
        }

        GeocodingResponse response;

        try
        {
            response = await openMeteoClient.SearchLocationsAsync(
                canonicalName,
                null,
                "en",
                null,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Open-Meteo timed out for city {CityName}",
                canonicalName);
            return CityGeocodingOutcome.ServiceUnavailable();
        }
        catch (ApiException exception)
        {
            logger.LogWarning(
                exception,
                "Open-Meteo returned an error for city {CityName}",
                canonicalName);
            return CityGeocodingOutcome.ServiceUnavailable();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Open-Meteo transport failed for city {CityName}",
                canonicalName);
            return CityGeocodingOutcome.ServiceUnavailable();
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(
                exception,
                "Open-Meteo timed out for city {CityName}",
                canonicalName);
            return CityGeocodingOutcome.ServiceUnavailable();
        }

        var location = response.Results?.FirstOrDefault(result =>
            StringComparer.OrdinalIgnoreCase.Equals(
                result.Name,
                canonicalName));

        if (location is null)
        {
            return CityGeocodingOutcome.GeocodingNotFound();
        }

        var record = new GeocodingCacheRecord(
            normalizedName,
            canonicalName,
            location.Country,
            location.Latitude,
            location.Longitude,
            location.Population,
            timeProvider.GetUtcNow());

        await cacheRepository.UpsertAsync(record, cancellationToken);
        return CityGeocodingOutcome.Success(record);
    }
}
