using Demo.Cities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDemoCities();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/city", (ICityService cityService) =>
    TypedResults.Ok(cityService.GetCityNames()));

app.MapGet("/city/usa", (IUsaCityService cityService) =>
    TypedResults.Ok(cityService.GetCityNames()));

app.Run();

public partial class Program;
