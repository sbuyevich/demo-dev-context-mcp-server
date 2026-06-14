namespace STI.City.API.Endpoints;

public sealed record CityLocationResponse(
    string CityName,
    string Country,
    double Latitude,
    double Longitude);
