namespace STI.City.API.Endpoints;

/// <summary>
/// Maps the <c>/city</c> route group. Stage 1 establishes the empty group;
/// later stages add the list and detail endpoint handlers.
/// </summary>
public static class CityEndpoints
{
    public static RouteGroupBuilder MapCityEndpoints(this IEndpointRouteBuilder routes)
    {
        var city = routes.MapGroup("/city");

        // Endpoints are mapped here in later stages:
        //   GET /city
        //   GET /city/usa
        //   GET /city/{cityName}/location
        //   GET /city/{cityName}/population

        return city;
    }
}
