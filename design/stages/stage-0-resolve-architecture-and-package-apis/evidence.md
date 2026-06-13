# Stage 0 Evidence

## Status

Stage 0 is complete. Company architecture, package contracts, and SQLite
data-access behavior are resolved. No solution has been scaffolded.

## Confirmed Packages

| Package | Library ID | Version | Package asset | City target |
| --- | --- | --- | --- | --- |
| `Demo.Cities` | `nuget:qa/Demo.Cities` | `1.1.0` | `net10.0` | `net10.0` |
| `OpenMeteo.Api.Client` | `nuget:prod/OpenMeteo.Api.Client` | `1.0.0` | `net10.0` | `net10.0` |
| `Formula.SimpleRepo` | `nuget:public/Formula.SimpleRepo` | `2.8.1` | `net8.0` | `net10.0` compatible |
| `Microsoft.Data.Sqlite` | public NuGet | `10.0.9` | `net10.0` compatible | `net10.0` |
| `Dapper` | public NuGet | `2.1.35` | transitive through SimpleRepo | `net10.0` compatible |

The QA `Demo.Cities` package is required because the production package does
not provide the QA-only U.S. city service.

## Demo.Cities Contract

Registration:

```csharp
IServiceCollection AddDemoCities(this IServiceCollection services);
```

Services:

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

Both methods document that results are returned in alphabetical order.

Evidence:

- `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.ServiceCollectionExtensions.AddDemoCities`
- `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.ICityService`
- `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.ICityService.GetCityNames`
- `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.IUsaCityService`
- `nuget://qa/Demo.Cities/1.1.0/symbol/Demo.Cities.IUsaCityService.GetCityNames`

## OpenMeteo.Api.Client Contract

Registration:

```csharp
IHttpClientBuilder AddOpenMeteoApiClient(
    this IServiceCollection services);

IHttpClientBuilder AddOpenMeteoApiClient(
    this IServiceCollection services,
    Action<HttpClient> configureClient);
```

Geocoding:

```csharp
Task<GeocodingResponse> SearchLocationsAsync(
    string name,
    int? count,
    string language,
    Format? format,
    CancellationToken cancellationToken);
```

Required response members:

```csharp
ICollection<LocationResult> GeocodingResponse.Results { get; set; }
string LocationResult.Name { get; set; }
string LocationResult.Country { get; set; }
double LocationResult.Latitude { get; set; }
double LocationResult.Longitude { get; set; }
int? LocationResult.Population { get; set; }
```

The generated client exposes `ApiException` and `ApiException<T>` for API
failures. The cancellation-aware overload must be used by the City API.

Evidence:

- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.ServiceCollectionExtensions.AddOpenMeteoApiClient`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.IOpenMeteoClient.SearchLocationsAsync`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.GeocodingResponse.Results`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.LocationResult.Name`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.LocationResult.Country`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.LocationResult.Latitude`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.LocationResult.Longitude`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.LocationResult.Population`
- `nuget://prod/OpenMeteo.Api.Client/1.0.0/symbol/OpenMeteo.Api.Client.ApiException`

## Formula.SimpleRepo Contract

Formula.SimpleRepo 2.8.1 ships a `net8.0` assembly, which is compatible with
the City API's `net10.0` target. The package asset target is not an
implementation blocker.

Compatibility was verified with .NET SDK 10.0.301 by restoring and building a
temporary `net10.0` class library with a `PackageReference` to
`Formula.SimpleRepo` 2.8.1. Restore and build succeeded with zero warnings and
zero errors.

DevContext queries must still use `targetFramework: net8.0` because that is the
package assembly indexed by DevContext. This query target describes the
package asset, not the consuming City application's target framework.

Repository definition:

```csharp
public abstract class RepositoryBase<TModel, TConstraints>
{
    protected RepositoryBase(IConfiguration config);
}
```

Connection mapping:

```csharp
public ConnectionDetails(
    string connectionName,
    Type connectionType,
    Dialect dialect);
```

Repository registration:

```csharp
IServiceCollection AddRepositoriesInAssembly(
    this IServiceCollection services,
    Assembly assembly);

IServiceCollection AddRepositoryByType(
    this IServiceCollection services,
    Type repositoryAssemblyType);
```

Repositories discovered through `AddRepositoriesInAssembly` or
`AddRepositoryByType` are registered with transient lifetime.

Relevant operations include:

```csharp
Task<TModel> GetAsync(
    object id,
    IDbTransaction transaction,
    int? commandTimeout);

Task<IEnumerable<TModel>> GetAsync(
    Hashtable constraints,
    IDbTransaction transaction,
    int? commandTimeout);

Task<int?> InsertAsync(
    TModel entityToInsert,
    IDbTransaction transaction,
    int? commandTimeout);

Task<int> UpdateAsync(
    TModel entityToUpdate,
    IDbTransaction transaction,
    int? commandTimeout,
    CancellationToken? token);
```

### SQLite implementation contract

The cache model must use the package's verified SQLite mapping:

```csharp
[ConnectionDetails(
    "CityCache",
    typeof(SqliteConnection),
    SimpleCRUD.Dialect.SQLite)]
[Table("GeocodingCache")]
public sealed class GeocodingCacheRecord
{
    [Key]
    public string NormalizedCityName { get; set; }
    // Remaining cache fields...
}
```

The repository must be decorated with `[Repo]`, derive from
`RepositoryBase<GeocodingCacheRecord, GeocodingCacheRecord>`, and be registered
with:

```csharp
services.AddRepositoryByType(
    typeof(SimpleRepoGeocodingCacheRepository));
```

Formula.SimpleRepo has no schema migration or upsert abstraction. Its own
SQLite tests create schemas with `SqliteConnection` and Dapper SQL. The City
implementation will therefore:

- add `Microsoft.Data.Sqlite` as the SQLite provider;
- create the table idempotently with `CREATE TABLE IF NOT EXISTS`;
- perform reads through inherited SimpleRepo operations; and
- implement one atomic SQLite
  `INSERT ... ON CONFLICT(NormalizedCityName) DO UPDATE` statement inside the
  SimpleRepo-derived repository.

`ReadOnlyRepositoryBase<TModel,TConstraints>` exposes the configured
`IDbConnection` as the protected `_connection` field, so the custom repository
can execute schema and upsert SQL against the same package-configured
connection. Dapper 2.1.35 is already a dependency of Formula.SimpleRepo 2.8.1;
the City project should reference it directly because City code calls
`ExecuteAsync`.

`Microsoft.Data.Sqlite` 10.0.9 was resolved from public NuGet and compiled in a
temporary `net10.0` project with zero warnings and zero errors.

Evidence:

- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryBase%602`
- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.ConnectionDetails.ConnectionDetails`
- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryConfiguration.AddRepositoriesInAssembly`
- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryConfiguration.AddRepositoryByType`
- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.IReadOnlyRepository%601.GetAsync`
- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryBase%602.InsertAsync`
- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Formula.SimpleRepo.RepositoryBase%602.UpdateAsync`
- `nuget://public/Formula.SimpleRepo/2.8.1/symbol/Dialect.SQLite`

Package-pinned source evidence:

- Repository registration uses `AddTransient`:
  <https://github.com/NephosIntegration/Formula.SimpleRepo/blob/fb755d98e46228555b0314e44eb69bf9c67d4791/Formula.SimpleRepo/Extensions/RepositoryConfiguration.cs>
- SQLite model mapping:
  <https://github.com/NephosIntegration/Formula.SimpleRepo/blob/fb755d98e46228555b0314e44eb69bf9c67d4791/Formula.SimpleRepo.Tests/Helpers/TodoModel.cs>
- SQLite schema creation with Dapper:
  <https://github.com/NephosIntegration/Formula.SimpleRepo/blob/fb755d98e46228555b0314e44eb69bf9c67d4791/Formula.SimpleRepo.Tests/Helpers/DatabasePrimer.cs>
- Protected configured connection:
  <https://github.com/NephosIntegration/Formula.SimpleRepo/blob/fb755d98e46228555b0314e44eb69bf9c67d4791/Formula.SimpleRepo/Base/ReadOnlyRepositoryBase.cs>

## Company API Architecture

Source: `docs://company-docs/api.architecture.md`

Create the solution for the `City` API with these projects:

| Project | Responsibility |
| --- | --- |
| `STI.City.API` | Minimal API endpoints, startup, dependency injection registration, configuration, and Serilog setup |
| `STI.City.Core` | Domain models, service contracts, service implementations, and repository contracts |
| `STI.City.Data` | Database entities, persistence mappings, and repository implementations using Formula.SimpleRepo |
| `STI.City.Tests` | Service unit tests and focused API and repository tests |

Dependency direction:

- `STI.City.API` references `STI.City.Core` and `STI.City.Data`.
- `STI.City.Data` references `STI.City.Core`.
- `STI.City.Core` references neither `STI.City.API` nor
  `STI.City.Data` and has no database-framework dependencies.
- Minimal API handlers delegate business logic to Core services and do not
  access repositories directly.

Boundary rules:

- Define interfaces for injected or replaceable services.
- Keep business rules in Core services.
- Define repository interfaces in Core and implement them in Data.
- Keep database entities separate from API request and response models.
- Map database entities to domain models at the Data boundary.

Testing rules:

- Unit-test Core services through service interfaces and mocked repository
  contracts.
- Test API routing, validation, authorization, and response mapping.
- Add integration tests for repository queries and database mappings.
