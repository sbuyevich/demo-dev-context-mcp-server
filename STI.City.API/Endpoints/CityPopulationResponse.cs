namespace STI.City.API.Endpoints;

public sealed record CityPopulationResponse(
    string CityName,
    string Country,
    long Population);
