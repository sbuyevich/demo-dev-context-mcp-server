using Demo.Cities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OpenMeteo.Api.Client;

namespace STI.City.Tests.Integration;

internal sealed class CityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"city-api-{Guid.NewGuid():N}.db");

    public CityApiFactory()
    {
        Cities = new Mock<ICityService>(MockBehavior.Strict);
        UsaCities = new Mock<IUsaCityService>(MockBehavior.Strict);
        OpenMeteo = new Mock<IOpenMeteoClient>(MockBehavior.Strict);
    }

    public Mock<ICityService> Cities { get; }

    public Mock<IUsaCityService> UsaCities { get; }

    public Mock<IOpenMeteoClient> OpenMeteo { get; }

    public string ConnectionString => $"Data Source={_databasePath}";

    public void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:CityCache",
            ConnectionString);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICityService>();
            services.RemoveAll<IUsaCityService>();
            services.RemoveAll<IOpenMeteoClient>();
            services.AddSingleton(Cities.Object);
            services.AddSingleton(UsaCities.Object);
            services.AddSingleton(OpenMeteo.Object);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DeleteDatabase();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabase();
        GC.SuppressFinalize(this);
    }
}
