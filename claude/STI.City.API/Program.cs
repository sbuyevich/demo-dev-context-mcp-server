using Demo.Cities;
using OpenMeteo.Api.Client;
using Serilog;
using STI.City.API.Configuration;
using STI.City.API.Endpoints;
using STI.City.Data.DependencyInjection;
using STI.City.Data.Schema;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Fail fast when the cache connection string is missing or blank.
var cacheConnectionString = builder.Configuration.GetRequiredCacheConnectionString();

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

// Verified package dependency-injection extensions.
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();

// Application data layer (SQLite cache repository).
builder.Services.AddCityData();

var app = builder.Build();

// Initialize the SQLite schema before the application accepts requests.
await GeocodingCacheSchemaInitializer.InitializeAsync(cacheConnectionString);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

// Map the /city route group. Endpoint handlers are added in later stages.
app.MapCityEndpoints();

app.Run();

/// <summary>Exposes the entry point to the integration test project.</summary>
public partial class Program;
