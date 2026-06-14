using Demo.Cities;
using OpenMeteo.Api.Client;
using Serilog;
using STI.City.Data;

var builder = WebApplication.CreateBuilder(args);

var cityCacheConnectionString =
    builder.Configuration.GetConnectionString("CityCache");

if (string.IsNullOrWhiteSpace(cityCacheConnectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:CityCache must be configured.");
}

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();
builder.Services.AddCityData();

var app = builder.Build();

await app.Services
    .GetRequiredService<IGeocodingCacheInitializer>()
    .InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.MapGroup("/city");

app.Run();

public partial class Program;
