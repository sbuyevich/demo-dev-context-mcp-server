# Stage 2: Implement the SQLite Cache

## Goal

Provide persistent, unique, and testable geocoding cache storage through
SQLite and verified `Formula.SimpleRepo` APIs.

## Dependencies

- Stage 1 exit criteria must pass.
- Repository interfaces must be coordinated with Stage 3 before parallel work
  begins.

## Implementation Status

**Complete.** The Core cache contract, SimpleRepo-backed SQLite repository,
idempotent startup schema initialization, atomic upsert, dependency injection,
and isolated repository integration tests are implemented.

The agreed Stage 3 repository contract is:

```csharp
Task<GeocodingCacheRecord?> GetAsync(
    string normalizedCityName,
    CancellationToken cancellationToken = default);

Task UpsertAsync(
    GeocodingCacheRecord record,
    CancellationToken cancellationToken = default);
```

## Work

- [x] Define `GeocodingCacheRecord` with normalized city name, display name,
  country, latitude, longitude, nullable population, and retrieval timestamp.
- [x] Create the `GeocodingCache` table and constraints from the
  specification.
- [x] Implement idempotent schema initialization before the application
  accepts requests.
- [x] Define `IGeocodingCacheRepository`.
- [x] Implement cache lookup and atomic insert/upsert using only verified
  `Formula.SimpleRepo` APIs.
- [x] Register the repository with the transient lifetime required by the
  package.

## Deliverables

- SQLite schema and startup initializer.
- Working SimpleRepo-backed cache repository.
- Repository tests using isolated SQLite databases.

## Exit Criteria

- [x] Schema initialization can run repeatedly without error.
- [x] Lookup returns the record for a normalized city name.
- [x] Upsert leaves exactly one row per normalized city name.
- [x] All fields, including nullable population and UTC timestamp, round-trip
  correctly.
- [x] SQLite failures remain distinguishable from cache misses.

## Verification

- Build: succeeded with zero warnings and zero errors.
- Formatting: `dotnet format --verify-no-changes` succeeded.
- Tests: 9 passed, 0 failed.
- Repository coverage includes transient lifetime, schema idempotency,
  round-trip mapping, atomic update uniqueness, database constraints, and
  cache-miss versus SQLite-failure behavior.
