using Demo.Cities;
using Microsoft.AspNetCore.Mvc;
using STI.City.Core.Geocoding;

namespace STI.City.API.Endpoints;

public static class CityEndpoints
{
    public static IEndpointRouteBuilder MapCityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/city");

        group.MapGet(
                "",
                (ICityService service) =>
                    Results.Json(service.GetCityNames()))
            .WithName("GetCities")
            .Produces<IReadOnlyList<string>>();

        group.MapGet(
                "/usa",
                (IUsaCityService service) =>
                    Results.Json(service.GetCityNames()))
            .WithName("GetUsaCities")
            .Produces<IReadOnlyList<string>>();

        group.MapGet(
                "/{cityName}/location",
                GetLocationAsync)
            .WithName("GetCityLocation")
            .Produces<CityLocationResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status502BadGateway)
            .Produces<ProblemDetails>(
                StatusCodes.Status500InternalServerError);

        group.MapGet(
                "/{cityName}/population",
                GetPopulationAsync)
            .WithName("GetCityPopulation")
            .Produces<CityPopulationResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status502BadGateway)
            .Produces<ProblemDetails>(
                StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> GetLocationAsync(
        string cityName,
        ICityGeocodingService service,
        HttpContext context)
    {
        var outcome = await service.GetAsync(
            cityName,
            context.RequestAborted);

        if (outcome.Kind == CityGeocodingOutcomeKind.Success)
        {
            var record = outcome.Record!;
            return Results.Json(
                new CityLocationResponse(
                    record.DisplayName,
                    record.Country,
                    record.Latitude,
                    record.Longitude));
        }

        return ToProblem(outcome.Kind, context);
    }

    private static async Task<IResult> GetPopulationAsync(
        string cityName,
        ICityGeocodingService service,
        HttpContext context)
    {
        var outcome = await service.GetAsync(
            cityName,
            context.RequestAborted);

        if (outcome.Kind != CityGeocodingOutcomeKind.Success)
        {
            return ToProblem(outcome.Kind, context);
        }

        var record = outcome.Record!;
        if (record.Population is null)
        {
            return Problem(
                StatusCodes.Status404NotFound,
                "Population not found",
                context);
        }

        return Results.Json(
            new CityPopulationResponse(
                record.DisplayName,
                record.Country,
                record.Population.Value));
    }

    private static IResult ToProblem(
        CityGeocodingOutcomeKind kind,
        HttpContext context) =>
        kind switch
        {
            CityGeocodingOutcomeKind.CityNotFound =>
                Problem(404, "City not found", context),
            CityGeocodingOutcomeKind.GeocodingNotFound =>
                Problem(404, "Geocoding result not found", context),
            CityGeocodingOutcomeKind.ServiceUnavailable =>
                Problem(
                    502,
                    "Geocoding service unavailable",
                    context),
            _ => throw new InvalidOperationException(
                $"Unsupported outcome {kind}.")
        };

    private static IResult Problem(
        int statusCode,
        string title,
        HttpContext context) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            });
}
