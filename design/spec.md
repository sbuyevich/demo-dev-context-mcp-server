# City Technical Specification

## 1. Purpose

Implement an API named `City` as a .NET 10 ASP.NET Core Minimal API that:

- returns city lists supplied by `Demo.Cities`;
- retrieves city geocoding details through `OpenMeteo.Api.Client`;
- stores geocoding results in SQLite through `Formula.SimpleRepo`; and
- reuses one cached geocoding record for both location and population requests.

This specification translates the requirements in `design/brd.md` into an
implementation and test contract.

## 2. Scope

### In scope

- Four anonymous HTTP GET endpoints under `/city`.
- JSON success and error responses.
- Case-insensitive city matching against package-provided city names.
- Persistent SQLite cache-aside behavior.
- Automated unit and HTTP integration tests.
- Dependency injection for package clients, repositories, and application
  services.

### Out of scope

- Authentication and authorization.
- City search, paging, filtering, or write endpoints.
- Cache expiration, refresh, or administrative cache invalidation.
- Resolving ambiguous cities that share a name.
- Weather forecast APIs.

## 3. API Architecture

The API product/service name is `City`.

Follow `docs://company-docs/api.architecture.md` and create:

| Project | Responsibility |
| --- | --- |
| `STI.City.API` | Minimal API endpoints, startup, DI, configuration, Serilog, and API DTOs |
| `STI.City.Core` | Domain models, service contracts and implementations, and repository contracts |
| `STI.City.Data` | SQLite entities, mappings, and Formula.SimpleRepo repository implementations |
| `STI.City.Tests` | Unit, API, and repository integration tests |

Reference direction:

- `STI.City.API` references `STI.City.Core` and `STI.City.Data`.
- `STI.City.Data` references `STI.City.Core`.
- `STI.City.Core` has no API, Data, or database-framework dependency.

Use `net10.0` for all projects. Keep business logic in Core services,
repository interfaces in Core, persistence implementations in Data, and
endpoint handlers thin. Database entities and API DTOs remain separate and
map through Core domain models.

## 4. Dependencies

Use these package sources and versions unless package compatibility requires a
documented change:

| Package | Source | Version | Purpose |
| --- | --- | --- | --- |
| `Demo.Cities` | QA | `1.1.0` | Global and U.S. city lists |
| `OpenMeteo.Api.Client` | Production | `1.0.0` | Open-Meteo geocoding client |
| `Formula.SimpleRepo` | Public | `2.8.1` | Repository access; `net8.0` package asset verified compatible with `net10.0` |
| `Microsoft.Data.Sqlite` | Public | `10.0.9` | SQLite provider |
| `Dapper` | Public | `2.1.35` | Schema and atomic SQLite upsert inside the SimpleRepo repository |
| `Microsoft.AspNetCore.Mvc.Testing` | Public | .NET 10 compatible | HTTP integration tests |

Register package services through each package's dependency-injection
extensions. Do not manually construct package service implementations or the
Open-Meteo HTTP client.

### Package API verification status

The `Demo.Cities` and `OpenMeteo.Api.Client` contracts are verified.
`Formula.SimpleRepo` query and CRUD contracts are verified against its
`net8.0` asset. A temporary `net10.0` project restored and built successfully
with Formula.SimpleRepo 2.8.1, so target-framework compatibility is confirmed
and is not a blocker. `SimpleCRUD.Dialect.SQLite`, transient repository
registration, provider-level schema initialization, and an atomic SQLite
`ON CONFLICT` upsert inside the SimpleRepo-derived repository are also
resolved. See
`design/stages/stage-0-resolve-architecture-and-package-apis/evidence.md`.

Stage 0 architecture and package verification is complete.

The verified `Demo.Cities` contracts are:

```csharp
public interface ICityService
{
    IReadOnlyList<string> GetCityNames();
}

public interface IUsaCityService
{
    IReadOnlyList<string> GetCityNames();
}
```

Both methods document that their returned names are already in alphabetical
order.

Register these services with:

```csharp
builder.Services.AddDemoCities();
```

The verified Open-Meteo registration and operation are:

```csharp
builder.Services.AddOpenMeteoApiClient();

Task<GeocodingResponse> SearchLocationsAsync(
    string name,
    int? count,
    string language,
    Format? format,
    CancellationToken cancellationToken);
```

`GeocodingResponse.Results` is an `ICollection<LocationResult>`.
`LocationResult` exposes `Name`, `Country`, `Latitude`, `Longitude`, and
nullable `int` `Population`. API failures use `ApiException`.

## 5. Configuration

Define the SQLite connection through configuration:

```json
{
  "ConnectionStrings": {
    "CityCache": "Data Source=city-cache.db"
  }
}
```

Requirements:

- Fail application startup when `ConnectionStrings:CityCache` is absent or
  blank.
- Keep the production database outside publicly served content.
- Allow tests to replace the connection string and package services.
- Do not include credentials or environment-specific absolute paths in source.

## 6. HTTP API Contract

All endpoints:

- are anonymous;
- return `application/json`;
- support cancellation through `HttpContext.RequestAborted`;
- return Problem Details JSON for errors; and
- use typed Minimal API results where package response types permit it.

### 6.1 `GET /city`

Return `ICityService.GetCityNames()` without sorting or otherwise changing its
order.

Success:

```http
200 OK
Content-Type: application/json
```

```json
["Chicago", "London", "Tokyo"]
```

### 6.2 `GET /city/usa`

Return `IUsaCityService.GetCityNames()` without sorting or otherwise changing
its order.

Success:

```http
200 OK
Content-Type: application/json
```

```json
["Chicago", "New York", "Seattle"]
```

The static `/usa` route must be mapped so it is not interpreted as a
`{cityName}` value.

### 6.3 `GET /city/{cityName}/location`

Return the cached or retrieved location for the matched canonical city.

Success:

```http
200 OK
Content-Type: application/json
```

```json
{
  "cityName": "New York",
  "country": "United States",
  "latitude": 40.7128,
  "longitude": -74.006
}
```

Response fields:

| Field | Type | Rules |
| --- | --- | --- |
| `cityName` | string | Canonical package-provided city name |
| `country` | string | Country returned by Open-Meteo |
| `latitude` | number | Open-Meteo latitude |
| `longitude` | number | Open-Meteo longitude |

### 6.4 `GET /city/{cityName}/population`

Return the population from the same cache record used by the location
endpoint.

Success:

```http
200 OK
Content-Type: application/json
```

```json
{
  "cityName": "New York",
  "country": "United States",
  "population": 8804190
}
```

`population` is a 64-bit integer in the API and persistence model. If the
selected Open-Meteo result has no population value, treat population as
unavailable and return `404 Not Found` from this endpoint. The location
endpoint may still return `200 OK` from that same cached record.

### 6.5 Error responses

Use `application/problem+json` with the standard Problem Details fields.
Include the request trace identifier in `extensions.traceId`.

| Condition | Status | Title |
| --- | --- | --- |
| Route value is empty after trimming | `404` | `City not found` |
| City is absent from `ICityService` | `404` | `City not found` |
| Open-Meteo returns no exact city-name match | `404` | `Geocoding result not found` |
| Population is absent on the selected result | `404` | `Population not found` |
| Open-Meteo fails and no cache record is available | `502` | `Geocoding service unavailable` |
| SQLite or another internal operation fails | `500` | `Internal server error` |

Do not expose exception messages, stack traces, connection strings, or
upstream response bodies in error responses.

## 7. City Matching and Normalization

For detail endpoints:

1. Use the route value after ASP.NET Core has URL-decoded it.
2. Trim leading and trailing whitespace.
3. Find the city in `ICityService.GetCityNames()` using
   `StringComparer.OrdinalIgnoreCase`.
4. If no city matches, return `404` without querying SQLite or Open-Meteo.
5. Use the package-provided spelling of the match as the canonical display
   name and Open-Meteo query.
6. Derive the cache key as `canonicalCityName.Trim().ToUpperInvariant()`.

Do not apply a second URL decode. Do not use culture-sensitive casing for
identity or cache keys.

Open-Meteo result selection must be deterministic:

1. Request geocoding results using the canonical city name.
2. Select the first result, in upstream order, whose returned name equals the
   canonical city name using `OrdinalIgnoreCase`.
3. If there is no exact name match, treat the result as missing.

Because `Demo.Cities` supplies no country discriminator, two places with the
same name cannot be disambiguated in this release. The first exact-name result
is the defined behavior.

## 8. Cache Design

### 8.1 Persistence model

Create one SQLite table named `GeocodingCache`:

| Column | SQLite type | Null | Constraint |
| --- | --- | --- | --- |
| `NormalizedCityName` | `TEXT` | No | Primary key |
| `DisplayName` | `TEXT` | No |  |
| `Country` | `TEXT` | No |  |
| `Latitude` | `REAL` | No | Range `-90` through `90` |
| `Longitude` | `REAL` | No | Range `-180` through `180` |
| `Population` | `INTEGER` | Yes | Non-negative when present |
| `RetrievedAtUtc` | `TEXT` | No | UTC ISO-8601 timestamp |

The primary key enforces one record per normalized city name. Initialize the
schema idempotently during application startup before accepting requests.

There is no time-to-live in this release. A successfully cached result remains
valid until the database is removed or changed by a future cache-management
feature.

### 8.2 Cache-aside algorithm

The application geocoding service must:

1. Validate and canonicalize the city against `ICityService`.
2. Query `GeocodingCache` by `NormalizedCityName`.
3. Return the record immediately on a cache hit.
4. Call `IOpenMeteoClient` on a cache miss.
5. Select the geocoding result using the rule in section 7.
6. Map it to one cache record and set `RetrievedAtUtc` from an injected
   `TimeProvider`.
7. Insert or upsert the record.
8. Return the mapped record.

Both detail endpoints must call this same application service method. They
must not maintain separate location and population caches.

Execute an atomic SQLite
`INSERT ... ON CONFLICT(NormalizedCityName) DO UPDATE` statement through
Dapper inside the SimpleRepo-derived repository. The primary key and atomic
statement ensure concurrent misses converge on one valid cache record.
Duplicate upstream calls during a simultaneous first lookup are acceptable.

### 8.3 Failure behavior

- A cache hit takes precedence over calling Open-Meteo, even if the external
  service is unavailable.
- Cancellation caused by the client disconnecting propagates as cancellation
  and is not translated to `502`.
- A documented Open-Meteo transport, timeout, or unsuccessful-response failure
  maps to `502` only when no cache record was found.
- An empty successful Open-Meteo response maps to `404`, not `502`.
- SQLite failures are internal failures and map to `500`.
- Log failures with structured city and operation fields, but do not log
  connection strings or full upstream response bodies.

## 9. Application Components

Place these logical components in the projects and folders required by the
verified company API architecture:

| Component | Responsibility |
| --- | --- |
| `CityEndpoints` | Map `/city` routes and translate service outcomes to HTTP results |
| `ICityGeocodingService` | Expose one cached geocoding lookup operation |
| `CityGeocodingService` | Validate city, perform cache-aside lookup, select upstream result |
| `IGeocodingCacheRepository` | Abstract cache retrieval and atomic insert/upsert |
| `SimpleRepoGeocodingCacheRepository` | Implement SQLite access with `Formula.SimpleRepo` |
| `GeocodingCacheRecord` | Internal persistence/application record |
| Response DTOs | Stable public JSON contracts, separate from package and database models |

Recommended lifetimes:

- package registrations: use their documented lifetimes;
- `CityGeocodingService`: scoped;
- SimpleRepo repository: transient, matching `AddRepositoryByType`;
- `TimeProvider.System`: singleton.

The route handlers must not contain SQL, package response traversal, or
cache-aside branching.

## 10. Startup and Middleware

In `STI.City.API`, at minimum:

1. Register Problem Details.
2. Register package services through verified DI extensions.
3. Register the application service, repository, `TimeProvider`, and validated
   cache configuration.
4. Build the application.
5. Initialize the SQLite schema.
6. Enable centralized exception handling outside development.
7. Map the `/city` route group.
8. Run the application.

OpenAPI metadata is recommended for all four endpoints, including operation
names, success response types, and documented error statuses.

## 11. Testing Specification

Use test doubles for package interfaces and an isolated SQLite database for
each test or test collection. Integration tests in `STI.City.Tests` must
execute through `WebApplicationFactory<Program>` and `HttpClient`.

### 11.1 Required tests

| Area | Test |
| --- | --- |
| City list | `GET /city` returns the exact package list and order |
| U.S. list | `GET /city/usa` returns the exact QA package list and order |
| Encoding | An encoded city name such as `New%20York` resolves successfully |
| Case matching | Mixed-case input resolves to the canonical package spelling |
| Whitespace | Leading/trailing decoded whitespace does not change the match |
| Unknown city | A city absent from `ICityService` returns `404` and makes no cache or upstream call |
| Location miss | Cache miss calls Open-Meteo once, persists a record, and returns location |
| Population miss | Cache miss calls Open-Meteo once, persists a record, and returns population |
| Shared cache | Location followed by population for the same city makes one total upstream call |
| Reverse shared cache | Population followed by location makes one total upstream call |
| Persistent cache hit | A pre-seeded cache row returns without calling Open-Meteo |
| Empty upstream result | No exact Open-Meteo match returns `404` |
| Missing population | Location returns `200`; population returns `404`; neither causes a second upstream call |
| Upstream failure | Failure on a cache miss returns `502` Problem Details |
| Cached upstream failure | A cache hit returns `200` without exercising the failing upstream client |
| Cache uniqueness | Repeated/upserted lookup leaves one row for the normalized city |
| Internal failure | SQLite failure returns `500` without exposing exception details |
| JSON contract | Success field names and `Content-Type` match this specification |

### 11.2 Test assertions

Tests must assert:

- HTTP status code;
- content type;
- deserialized response body;
- canonical city spelling;
- package list ordering;
- upstream client call count and query value;
- cache row count and stored field values; and
- absence of upstream calls on cache hits and invalid cities.

## 12. Acceptance Checklist

Implementation is complete when:

- the solution builds with the .NET 10 SDK;
- the API product/service is named `City`;
- solution and project structure matches the cited DevContext company API
  architecture standard;
- all four routes implement the contracts in section 6;
- QA `Demo.Cities` supplies both city-list services;
- package-provided list order is unchanged;
- city matching is decoded, trimmed, and case-insensitive;
- one SQLite row supplies both detail endpoints;
- repeated cached requests do not call Open-Meteo;
- status mappings distinguish missing data, upstream failure, and internal
  failure;
- all required automated tests pass; and
- exact third-party API and DI usage is backed by DevContext evidence.

## 13. Evidence

- `Demo.Cities.ICityService`:
  `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.ICityService`
- `Demo.Cities.ICityService.GetCityNames`:
  `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.ICityService.GetCityNames`
- `Demo.Cities.IUsaCityService`:
  `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.IUsaCityService`
- `Demo.Cities.IUsaCityService.GetCityNames`:
  `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.IUsaCityService.GetCityNames`
- `Demo.Cities.ServiceCollectionExtensions.AddDemoCities`:
  `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.ServiceCollectionExtensions.AddDemoCities`
- `OpenMeteo.Api.Client.ServiceCollectionExtensions.AddOpenMeteoApiClient`:
  `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.ServiceCollectionExtensions.AddOpenMeteoApiClient`
- `OpenMeteo.Api.Client.IOpenMeteoClient.SearchLocationsAsync`:
  `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.IOpenMeteoClient.SearchLocationsAsync`
- `OpenMeteo.Api.Client.GeocodingResponse.Results`:
  `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.GeocodingResponse.Results`
- `OpenMeteo.Api.Client.LocationResult`:
  `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.LocationResult`
- `OpenMeteo.Api.Client.ApiException`:
  `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.ApiException`
- `Formula.SimpleRepo.RepositoryBase<TModel,TConstraints>`:
  `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryBase%602`
- `Formula.SimpleRepo.ConnectionDetails` constructor:
  `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.ConnectionDetails.ConnectionDetails`
- `Formula.SimpleRepo.RepositoryConfiguration.AddRepositoriesInAssembly`:
  `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryConfiguration.AddRepositoriesInAssembly`
- `Formula.SimpleRepo.IReadOnlyRepository.GetAsync`:
  `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.IReadOnlyRepository%601.GetAsync`
- `Formula.SimpleRepo.RepositoryBase.InsertAsync`:
  `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryBase%602.InsertAsync`
- `Formula.SimpleRepo.RepositoryBase.UpdateAsync`:
  `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryBase%602.UpdateAsync`
- ASP.NET Core Minimal APIs:
  <https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis>
- ASP.NET Core integration testing:
  <https://learn.microsoft.com/aspnet/core/test/integration-tests>
- ASP.NET Core error handling:
  <https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api>

- Company API architecture:
  `docs://company-docs/api.architecture.md`
- SimpleRepo's core repository APIs are indexed for its `net8.0` package
  asset, which is verified compatible with the City API's `net10.0` target.
  Its SQLite dialect, transient registration, provider-level schema creation,
  and repository-contained atomic upsert approach are resolved in the Stage 0
  evidence.
