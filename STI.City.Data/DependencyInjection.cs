using Formula.SimpleRepo;
using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Geocoding;
using STI.City.Data.Geocoding;

namespace STI.City.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddCityData(
        this IServiceCollection services)
    {
        services.AddRepositoryByType(
            typeof(SimpleRepoGeocodingCacheRepository));
        services.AddTransient<IGeocodingCacheRepository>(
            provider => provider.GetRequiredService<
                SimpleRepoGeocodingCacheRepository>());
        services.AddSingleton<
            IGeocodingCacheInitializer,
            GeocodingCacheInitializer>();

        return services;
    }
}
