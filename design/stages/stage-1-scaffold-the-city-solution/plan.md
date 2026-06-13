# Stage 1: Scaffold the City Solution

## Goal

Create a buildable .NET 10 solution that follows the verified company
architecture and has the required configuration and dependency registrations.

## Dependencies

- Stage 0 is complete.

## Implementation Status

**Complete.** The .NET 10 solution, project references, package sources,
configuration validation, dependency injection registrations, Serilog setup,
empty `/city` route group, and startup tests are implemented.

## Project Structure

Create a `City` solution containing:

| Project | Type | References |
| --- | --- | --- |
| `STI.City.API` | ASP.NET Core Minimal API | `STI.City.Core`, `STI.City.Data` |
| `STI.City.Core` | Class library | None |
| `STI.City.Data` | Class library | `STI.City.Core` |
| `STI.City.Tests` | Test project | `STI.City.API`, `STI.City.Core`, `STI.City.Data` |

Use matching root namespaces for each project. Keep domain models, service
interfaces and implementations, and repository interfaces in Core. Keep
SQLite entities, mappings, and SimpleRepo repository implementations in Data.
Keep endpoint DTOs, startup, configuration, DI registration, and Serilog in
API.

Architecture source: `docs://company-docs/api.architecture.md`.

## Work

- [x] Create the .NET 10 `City` solution and the four `STI.City.*` projects
  above.
- [x] Add project references exactly as defined in the project structure
  table.
- [x] Configure the QA and production NuGet sources as required.
- [x] Add the verified package versions.
- [x] Add `Microsoft.Data.Sqlite` 10.0.9 and an explicit `Dapper` 2.1.35
  reference for provider SQL used inside the SimpleRepo-derived repository.
- [x] Create the ASP.NET Core Minimal API host with
  `WebApplication.CreateBuilder`.
- [x] Expose `Program` to the integration test project.
- [x] Add `ConnectionStrings:CityCache` configuration and fail startup when
  it is missing or blank.
- [x] Register Problem Details, `TimeProvider.System`, and the verified
  package dependency-injection extensions.

## Deliverables

- Buildable `City` solution containing all four company-standard projects.
- Minimal host with configuration and dependency injection wiring.
- Empty `/city` route group ready for endpoint mapping.

## Exit Criteria

- [x] `dotnet restore` succeeds from the configured package sources.
- [x] `dotnet build` succeeds with no warnings, package errors, or
  project-reference errors.
- [x] A host smoke test starts the application with valid configuration.
- [x] Startup tests prove missing and blank cache configuration fail fast.

## Verification

- .NET SDK: 10.0.301
- Restore: succeeded for all four projects.
- Build: succeeded with zero warnings and zero errors.
- Tests: 3 passed, 0 failed.
