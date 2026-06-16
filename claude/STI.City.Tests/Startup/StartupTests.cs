using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using STI.City.API.Configuration;

namespace STI.City.Tests.Startup;

/// <summary>
/// Stage 1 exit criteria: the host starts and routes requests with valid
/// configuration, and configuration validation fails fast when the cache
/// connection string is missing or blank.
/// </summary>
public sealed class StartupTests
{
    [Fact]
    public async Task Application_StartsAndRoutesRequests_WithValidConfiguration()
    {
        // The connection string is read at builder construction (before the host
        // is built), so an environment variable is the reliable override; it
        // keeps the startup-created database out of the project directory.
        var dbPath = Path.Combine(Path.GetTempPath(), $"city-startup-{Guid.NewGuid():N}.db");
        Environment.SetEnvironmentVariable("ConnectionStrings__CityCache", $"Data Source={dbPath};Pooling=False");
        try
        {
            await using var factory = new WebApplicationFactory<Program>();

            using var client = factory.CreateClient();
            var response = await client.GetAsync("/city");

            // The /city group exists but maps no endpoints yet, so a started host
            // routes the request to a 404.
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__CityCache", null);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public void Configuration_FailsFast_WhenConnectionStringMissing()
    {
        var configuration = BuildConfiguration(settings: new());

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetRequiredCacheConnectionString);

        Assert.Contains("ConnectionStrings:CityCache", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Configuration_FailsFast_WhenConnectionStringBlank(string blank)
    {
        var configuration = BuildConfiguration(new()
        {
            ["ConnectionStrings:CityCache"] = blank,
        });

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetRequiredCacheConnectionString);

        Assert.Contains("ConnectionStrings:CityCache", exception.Message);
    }

    [Fact]
    public void Configuration_ReturnsConnectionString_WhenPresent()
    {
        var configuration = BuildConfiguration(new()
        {
            ["ConnectionStrings:CityCache"] = "Data Source=city-cache.db",
        });

        Assert.Equal("Data Source=city-cache.db", configuration.GetRequiredCacheConnectionString());
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
