using Demo.Cities;
using Microsoft.Extensions.Logging;
using OpenMeteo.Api.Client;

namespace STI.City.Core.Geocoding;

public sealed class CityGeocodingService(
    ICityService cityService,
    IOpenMeteoClient openMeteoClient,
    IGeocodingCacheRepository cacheRepository,
    TimeProvider timeProvider,
    ILogger<CityGeocodingService> logger)
    : ICityGeocodingService
{
    public async Task<CityGeocodingResult> GetAsync(
        string cityName,
        CancellationToken cancellationToken = default)
    {
        var requestedCityName = cityName?.Trim();
        if (string.IsNullOrEmpty(requestedCityName))
        {
            return CityGeocodingResult.CityNotFound();
        }

        var canonicalCityName = cityService
            .GetCityNames()
            .FirstOrDefault(city => StringComparer.OrdinalIgnoreCase.Equals(
                city,
                requestedCityName));

        if (canonicalCityName is null)
        {
            return CityGeocodingResult.CityNotFound();
        }

        var normalizedCityName = canonicalCityName
            .Trim()
            .ToUpperInvariant();
        var cachedRecord = await cacheRepository.GetAsync(
            normalizedCityName,
            cancellationToken);

        if (cachedRecord is not null)
        {
            return CityGeocodingResult.Success(cachedRecord);
        }

        GeocodingResponse response;
        try
        {
            response = await openMeteoClient.SearchLocationsAsync(
                canonicalCityName,
                count: null,
                language: "en",
                format: Format.Json,
                cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Open-Meteo timed out while resolving {CityName}.",
                canonicalCityName);
            return CityGeocodingResult.ServiceUnavailable();
        }
        catch (ApiException exception)
        {
            logger.LogWarning(
                exception,
                "Open-Meteo returned status {StatusCode} while resolving {CityName}.",
                exception.StatusCode,
                canonicalCityName);
            return CityGeocodingResult.ServiceUnavailable();
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Open-Meteo transport failed while resolving {CityName}.",
                canonicalCityName);
            return CityGeocodingResult.ServiceUnavailable();
        }

        var location = response.Results?.FirstOrDefault(result =>
            StringComparer.OrdinalIgnoreCase.Equals(
                result.Name,
                canonicalCityName));

        if (location is null)
        {
            return CityGeocodingResult.GeocodingNotFound();
        }

        var record = new GeocodingCacheRecord(
            normalizedCityName,
            canonicalCityName,
            location.Country,
            location.Latitude,
            location.Longitude,
            location.Population,
            timeProvider.GetUtcNow());

        await cacheRepository.UpsertAsync(record, cancellationToken);
        return CityGeocodingResult.Success(record);
    }
}
