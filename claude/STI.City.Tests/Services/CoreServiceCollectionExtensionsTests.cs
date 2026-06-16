using Microsoft.Extensions.DependencyInjection;
using STI.City.Core.DependencyInjection;
using STI.City.Core.Services;

namespace STI.City.Tests.Services;

public sealed class CoreServiceCollectionExtensionsTests
{
    // Purpose: registers the city geocoding service with the scoped lifetime
    [Fact]
    public void AddCityCore_RegistersCityGeocodingService_AsScoped()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        var actual = services.AddCityCore();

        // assert
        Assert.Same(services, actual);
        var descriptor = Assert.Single(
            actual, service => service.ServiceType == typeof(ICityGeocodingService));
        Assert.Equal(typeof(CityGeocodingService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }
}
