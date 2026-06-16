using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.Repositories;
using STI.City.Data.Repositories;

namespace STI.City.Data.DependencyInjection;

/// <summary>
/// Registers the Data layer's SQLite cache repository.
/// </summary>
public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddCityData(this IServiceCollection services)
    {
        // Formula.SimpleRepo repositories require the transient lifetime.
        services.AddTransient<IGeocodingCacheRepository, SimpleRepoGeocodingCacheRepository>();
        return services;
    }
}
