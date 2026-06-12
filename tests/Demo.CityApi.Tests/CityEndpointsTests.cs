using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Demo.CityApi.Tests;

public sealed class CityEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetCityReturnsExactOrderedCityNames()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city");
        var cityNames = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(cityNames);
        Assert.Equal(
            ["berlin", "london", "paris", "tokyo", "toronto"],
            cityNames);
    }

    [Fact]
    public async Task GetUsaCityReturnsExactOrderedCityNames()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city/usa");
        var cityNames = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(cityNames);
        Assert.Equal(
            ["chicago", "houston", "los angeles", "new york", "philadelphia", "phoenix"],
            cityNames);
    }
}
