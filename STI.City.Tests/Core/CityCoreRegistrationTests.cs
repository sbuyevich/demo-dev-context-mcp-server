using Demo.Cities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core;
using STI.City.Core.Geocoding;

namespace STI.City.Tests.Core;

public sealed class CityCoreRegistrationTests
{
    [Fact]
    public void AddCityCore_ResolvedAcrossScopes_RegistersScopedService()
    {
        // Purpose: The geocoding service must use the documented scoped lifetime.
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ICityService>());
        services.AddSingleton(Mock.Of<IOpenMeteoClient>());
        services.AddSingleton(Mock.Of<IGeocodingCacheRepository>());
        services.AddSingleton(Mock.Of<TimeProvider>());
        services.AddSingleton(
            Mock.Of<ILogger<CityGeocodingService>>());
        services.AddCityCore();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        // act
        var actual = firstScope.ServiceProvider
            .GetRequiredService<ICityGeocodingService>();

        // assert
        var sameScope = firstScope.ServiceProvider
            .GetRequiredService<ICityGeocodingService>();
        var otherScope = secondScope.ServiceProvider
            .GetRequiredService<ICityGeocodingService>();
        Assert.IsType<CityGeocodingService>(actual);
        Assert.Same(actual, sameScope);
        Assert.NotSame(actual, otherScope);
    }
}
