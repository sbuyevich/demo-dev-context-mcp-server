using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace STI.City.Tests;

public sealed class StartupTests
{
    [Fact]
    public async Task Host_starts_with_valid_configuration()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/city");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Host_fails_when_city_cache_connection_string_is_invalid(
        string? connectionString)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    if (connectionString is null)
                    {
                        configuration.Sources.Clear();
                        return;
                    }

                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:CityCache"] = connectionString
                        });
                });
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            "ConnectionStrings:CityCache must be configured.",
            exception.ToString(),
            StringComparison.Ordinal);
    }
}
