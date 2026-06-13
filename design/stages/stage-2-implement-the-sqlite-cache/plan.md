# Stage 2: Implement the SQLite Cache

## Goal

Provide persistent, unique, and testable geocoding cache storage through
SQLite and verified `Formula.SimpleRepo` APIs.

## Dependencies

- Stage 1 exit criteria must pass.
- Repository interfaces must be coordinated with Stage 3 before parallel work
  begins.

## Work

- Define `GeocodingCacheRecord` with normalized city name, display name,
  country, latitude, longitude, nullable population, and retrieval timestamp.
- Create the `GeocodingCache` table and constraints from the specification.
- Implement idempotent schema initialization before the application accepts
  requests.
- Define `IGeocodingCacheRepository`.
- Implement cache lookup and atomic insert/upsert using only verified
  `Formula.SimpleRepo` APIs.
- Register the repository with the lifetime required by the package.

## Deliverables

- SQLite schema and startup initializer.
- Working SimpleRepo-backed cache repository.
- Repository tests using isolated SQLite databases.

## Exit Criteria

- Schema initialization can run repeatedly without error.
- Lookup returns the record for a normalized city name.
- Upsert leaves exactly one row per normalized city name.
- All fields, including nullable population and UTC timestamp, round-trip
  correctly.
- SQLite failures remain distinguishable from cache misses.
