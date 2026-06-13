using Demo.Cities;
using Microsoft.Extensions.Options;
using OpenMeteo.Api.Client;
using Serilog;
using STI.City.API.Configuration;
using STI.City.Core;
using STI.City.Data;
using STI.City.Data.Geocoding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services
    .AddOptions<CityCacheOptions>()
    .BindConfiguration("ConnectionStrings")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CityCache),
        "ConnectionStrings:CityCache must be configured.")
    .ValidateOnStart();
builder.Services.AddDemoCities();
builder.Services.AddOpenMeteoApiClient();
builder.Services.AddCityCore();
builder.Services.AddCityData();
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<CityCacheOptions>>().Value;
await app.Services
    .GetRequiredService<ICityCacheSchemaInitializer>()
    .InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseSerilogRequestLogging();

app.MapGroup("/city")
    .WithTags("City");

app.Run();

public partial class Program;
