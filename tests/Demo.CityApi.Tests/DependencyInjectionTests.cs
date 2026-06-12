using Demo.Cities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.CityApi.Tests;

public sealed class DependencyInjectionTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void CityServicesResolve()
    {
        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICityService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUsaCityService>());
    }
}
