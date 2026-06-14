using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenMeteo.Api.Client;
using STI.City.Core.Geocoding;

namespace STI.City.Tests.Integration;

public sealed class CityApiTests
{
    [Fact]
    public async Task CityListsPreservePackageOrder()
    {
        await using var factory = new CityApiFactory();
        factory.Cities.Setup(service => service.GetCityNames())
            .Returns(["Chicago", "London", "Tokyo"]);
        factory.UsaCities.Setup(service => service.GetCityNames())
            .Returns(["Chicago", "New York", "Seattle"]);
        using var client = factory.CreateClient();

        var cities = await client.GetFromJsonAsync<string[]>("/city");
        var usaCities =
            await client.GetFromJsonAsync<string[]>("/city/usa");

        Assert.NotNull(cities);
        Assert.Equal(["Chicago", "London", "Tokyo"], cities);
        Assert.NotNull(usaCities);
        Assert.Equal(
            ["Chicago", "New York", "Seattle"],
            usaCities);
        factory.Cities.VerifyAll();
        factory.UsaCities.VerifyAll();
    }

    [Theory]
    [InlineData("/city/New%20York/location")]
    [InlineData("/city/nEw%20yOrK/location")]
    [InlineData("/city/%20New%20York%20/location")]
    public async Task EncodedCaseInsensitiveAndTrimmedNamesResolve(
        string path)
    {
        await using var factory = CreateSuccessfulFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "New York",
            json.GetProperty("cityName").GetString());
        factory.OpenMeteo.Verify(
            openMeteo => openMeteo.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnknownCityReturns404WithoutUpstreamCall()
    {
        await using var factory = new CityApiFactory();
        factory.Cities.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync("/city/Unknown/location");
        var problem =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblem(problem, "City not found", 404);
        factory.OpenMeteo.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("location")]
    [InlineData("population")]
    public async Task CacheMissPersistsAndReturnsDetail(
        string detail)
    {
        await using var factory = CreateSuccessfulFactory();
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync($"/city/New%20York/{detail}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.OpenMeteo.Verify(
            openMeteo => openMeteo.SearchLocationsAsync(
                "New York",
                null,
                "en",
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        await using var connection =
            new SqliteConnection(factory.ConnectionString);
        var row = await connection.QuerySingleAsync<CacheRow>(
            "SELECT * FROM GeocodingCache;");
        Assert.Equal("NEW YORK", row.NormalizedCityName);
        Assert.Equal("New York", row.DisplayName);
        Assert.Equal(8_804_190, row.Population);
    }

    [Theory]
    [InlineData("location", "population")]
    [InlineData("population", "location")]
    public async Task DetailEndpointsShareOneCachedRecord(
        string first,
        string second)
    {
        await using var factory = CreateSuccessfulFactory();
        using var client = factory.CreateClient();

        var firstResponse =
            await client.GetAsync($"/city/New%20York/{first}");
        var secondResponse =
            await client.GetAsync($"/city/New%20York/{second}");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        factory.OpenMeteo.Verify(
            openMeteo => openMeteo.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await using var connection =
            new SqliteConnection(factory.ConnectionString);
        Assert.Equal(
            1,
            await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM GeocodingCache;"));
    }

    [Fact]
    public async Task PreseededCacheBypassesFailingUpstream()
    {
        await using var factory = new CityApiFactory();
        factory.Cities.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        factory.OpenMeteo.Setup(openMeteo =>
                openMeteo.SearchLocationsAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<string>(),
                    It.IsAny<Format?>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unavailable"));
        using var client = factory.CreateClient();
        await SeedAsync(factory, CreateRecord());

        using var response =
            await client.GetAsync("/city/New%20York/location");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        factory.OpenMeteo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmptyUpstreamResultReturns404()
    {
        await using var factory = CreateFactoryWithResponse(
            new GeocodingResponse { Results = [] });
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync("/city/New%20York/location");
        var problem =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblem(problem, "Geocoding result not found", 404);
    }

    [Fact]
    public async Task MissingPopulationIsCachedAndReturns404OnlyThere()
    {
        await using var factory = CreateFactoryWithResponse(
            Response(Location(population: null)));
        using var client = factory.CreateClient();

        using var location =
            await client.GetAsync("/city/New%20York/location");
        using var population =
            await client.GetAsync("/city/New%20York/population");
        var problem =
            await population.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, location.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, population.StatusCode);
        AssertProblem(problem, "Population not found", 404);
        factory.OpenMeteo.Verify(
            openMeteo => openMeteo.SearchLocationsAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<Format?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpstreamFailureReturns502ProblemDetails()
    {
        await using var factory = new CityApiFactory();
        factory.Cities.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        factory.OpenMeteo.Setup(openMeteo =>
                openMeteo.SearchLocationsAsync(
                    "New York",
                    null,
                    "en",
                    null,
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("secret upstream"));
        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync("/city/New%20York/location");
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(body).RootElement;

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        AssertProblem(
            problem,
            "Geocoding service unavailable",
            502);
        Assert.DoesNotContain("secret upstream", body);
    }

    [Fact]
    public async Task SqliteFailureReturnsSanitized500()
    {
        await using var factory = new CityApiFactory();
        factory.Cities.Setup(service => service.GetCityNames())
            .Returns(["New York"]);
        using var client = factory.CreateClient();
        factory.DeleteDatabase();

        using var response =
            await client.GetAsync("/city/New%20York/location");
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(body).RootElement;

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);
        AssertProblem(problem, "Internal server error", 500);
        Assert.DoesNotContain("GeocodingCache", body);
        Assert.DoesNotContain("Data Source", body);
        factory.OpenMeteo.VerifyNoOtherCalls();
    }

    private static CityApiFactory CreateSuccessfulFactory() =>
        CreateFactoryWithResponse(Response(Location()));

    private static CityApiFactory CreateFactoryWithResponse(
        GeocodingResponse response)
    {
        var factory = new CityApiFactory();
        factory.Cities.Setup(service => service.GetCityNames())
            .Returns(["London", "New York"]);
        factory.OpenMeteo.Setup(openMeteo =>
                openMeteo.SearchLocationsAsync(
                    "New York",
                    null,
                    "en",
                    null,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        return factory;
    }

    private static async Task SeedAsync(
        CityApiFactory factory,
        GeocodingCacheRecord record)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IGeocodingCacheRepository>()
            .UpsertAsync(record);
    }

    private static GeocodingCacheRecord CreateRecord() =>
        new(
            "NEW YORK",
            "New York",
            "United States",
            40.7128,
            -74.006,
            8_804_190,
            DateTimeOffset.UtcNow);

    private static GeocodingResponse Response(
        params LocationResult[] results) =>
        new() { Results = results };

    private static LocationResult Location(
        int? population = 8_804_190) =>
        new()
        {
            Name = "New York",
            Country = "United States",
            Latitude = 40.7128,
            Longitude = -74.006,
            Population = population
        };

    private static void AssertProblem(
        JsonElement problem,
        string title,
        int status)
    {
        Assert.Equal(title, problem.GetProperty("title").GetString());
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.False(
            string.IsNullOrWhiteSpace(
                problem.GetProperty("traceId").GetString()));
    }

    private sealed class CacheRow
    {
        public string NormalizedCityName { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public long? Population { get; init; }
    }
}
