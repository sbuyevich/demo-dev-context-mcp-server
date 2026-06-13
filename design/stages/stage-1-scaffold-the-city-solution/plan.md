# Stage 1: Scaffold the City Solution

## Goal

Create a buildable .NET 10 solution that follows the verified company
architecture and has the required configuration and dependency registrations.

## Dependencies

- Stage 0 exit criteria must pass.

## Work

- Create the .NET 10 solution and projects using the names and structure
  established in Stage 0.
- Add project references following the company-defined dependency direction.
- Configure the QA and production NuGet sources as required.
- Add the verified package versions.
- Add `Microsoft.Data.Sqlite` 10.0.9 and an explicit `Dapper` 2.1.35
  reference for provider SQL used inside the SimpleRepo-derived repository.
- Create the ASP.NET Core Minimal API host with
  `WebApplication.CreateBuilder`.
- Expose `Program` to the integration test project.
- Add `ConnectionStrings:CityCache` configuration and fail startup when it is
  missing or blank.
- Register Problem Details, `TimeProvider.System`, and the verified package
  dependency-injection extensions.

## Deliverables

- Buildable `City` solution and test projects.
- Minimal host with configuration and dependency injection wiring.
- Empty `/city` route group ready for endpoint mapping.

## Exit Criteria

- `dotnet restore` succeeds from the configured package sources.
- `dotnet build` succeeds with no package or project-reference errors.
- A host smoke test starts the application with valid configuration.
- A startup test proves missing cache configuration fails fast.
