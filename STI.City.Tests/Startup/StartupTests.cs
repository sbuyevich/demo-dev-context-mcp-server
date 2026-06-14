using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using STI.City.Data;

namespace STI.City.Tests.Startup;

public sealed class StartupTests
{
    [Fact]
    public async Task HostStartsWithValidCacheConfiguration()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            await using var factory = CreateFactory(
                $"Data Source={databasePath}");

            using var scope = factory.Services.CreateScope();

            Assert.NotNull(
                scope.ServiceProvider.GetRequiredService<
                    IGeocodingCacheInitializer>());
            Assert.True(File.Exists(databasePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingOrBlankCacheConfigurationFailsFast(
        string? connectionString)
    {
        using var factory = CreateFactory(connectionString);

        var exception = Assert.ThrowsAny<Exception>(
            () => _ = factory.CreateClient());

        Assert.Contains(
            "ConnectionStrings:CityCache must be configured.",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string? connectionString) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting(
                    "ConnectionStrings:CityCache",
                    connectionString ?? string.Empty);
                builder.ConfigureAppConfiguration(
                    (_, configuration) =>
                    {
                        configuration.Sources.Clear();
                        configuration.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["ConnectionStrings:CityCache"] =
                                    connectionString
                            });
                    });
            });

    private static string CreateDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"city-startup-{Guid.NewGuid():N}.db");
}
