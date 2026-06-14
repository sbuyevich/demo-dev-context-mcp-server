using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Geocoding;

namespace STI.City.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCityCore(
        this IServiceCollection services)
    {
        services.AddScoped<
            ICityGeocodingService,
            CityGeocodingService>();

        return services;
    }
}
