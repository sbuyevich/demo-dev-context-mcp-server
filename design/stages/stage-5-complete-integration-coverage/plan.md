# Stage 5: Complete Integration Coverage

## Goal

Verify the complete HTTP pipeline, dependency wiring, cache behavior, and
failure mappings with deterministic automated tests.

## Dependencies

- Stage 4 exit criteria must pass.

## Work

- Create a `WebApplicationFactory<Program>` test host following company test
  conventions.
- Replace package services with deterministic test doubles.
- Give each test or test collection an isolated SQLite database.
- Cover every scenario in section 11 of `design/spec.md`.
- Assert HTTP contracts, canonical names, list ordering, upstream call counts,
  cache contents, and cache row uniqueness.

## Deliverables

- Unit and HTTP integration test suites.
- Reusable test host and dependency overrides.
- Test data covering success, missing data, and dependency failures.

## Exit Criteria

- Location then population makes one total upstream call.
- Population then location makes one total upstream call.
- Pre-seeded cache records bypass Open-Meteo.
- Empty upstream results, unknown cities, missing population, upstream
  failures, and SQLite failures have the required responses.
- The full test suite passes repeatedly without shared database state.
