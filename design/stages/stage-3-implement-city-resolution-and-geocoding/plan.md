# Stage 3: Implement City Resolution and Geocoding

## Goal

Implement the shared cache-aside workflow that resolves a supported city and
supplies both location and population data.

## Dependencies

- Stage 2 is complete.
- Use `IGeocodingCacheRepository.GetAsync` and `UpsertAsync` with the
  normalized city key and request cancellation token.

## Implementation Status

**Complete.** The scoped geocoding service, explicit outcome model,
case-insensitive city resolution, invariant cache normalization, cache-aside
workflow, deterministic upstream selection, failure mapping, persistence, and
unit tests are implemented.

The Stage 4 outcome contract is:

| Outcome | Meaning |
| --- | --- |
| `Success` | `Record` contains the shared location and population data |
| `CityNotFound` | The trimmed route value is empty or not in `ICityService` |
| `GeocodingNotFound` | Open-Meteo returned no exact city-name match |
| `ServiceUnavailable` | Open-Meteo failed or timed out on a cache miss |

Persistence failures and request cancellation propagate for Stage 4 and the
ASP.NET Core pipeline to handle separately.

## Work

- [x] Define `ICityGeocodingService` with one operation that supplies the
  geocoding record used by both detail endpoints.
- [x] Match route city names against `ICityService.GetCityNames()` after
  trimming, using `StringComparer.OrdinalIgnoreCase`.
- [x] Preserve the package-provided spelling as the canonical display name.
- [x] Normalize cache keys with `Trim().ToUpperInvariant()`.
- [x] Return a not-found outcome before cache or upstream access for unknown
  cities.
- [x] On a cache miss, call the verified `IOpenMeteoClient` geocoding operation
  with the canonical name and request cancellation token.
- [x] Select the first upstream result whose city name exactly matches using
  `OrdinalIgnoreCase`.
- [x] Map and persist one record containing coordinates and population.
- [x] Map upstream transport, timeout, or unsuccessful-response failures to a
  service-unavailable outcome without swallowing request cancellation.

## Deliverables

- `CityGeocodingService` and explicit service outcome model.
- Test doubles for city services, Open-Meteo, cache repository, and time.
- Unit tests for normalization, result selection, cache-aside behavior, and
  failures.

## Exit Criteria

- [x] A cache hit never calls Open-Meteo.
- [x] A cache miss calls Open-Meteo once and persists the selected result.
- [x] Unknown cities call neither the cache nor Open-Meteo.
- [x] Empty or nonmatching upstream results produce a not-found outcome.
- [x] Upstream failures and request cancellation remain distinct.
- [x] One service result supplies both location and population data.

## Verification

- Restore: succeeded offline using the QA feed, production feed, and existing
  global package cache.
- Build: succeeded with zero warnings and zero errors.
- Formatting: `dotnet format --verify-no-changes` succeeded.
- Tests: 24 passed, 0 failed.
- Service coverage includes scoped registration, normalization, cache hits,
  exact-match selection, nullable population, token propagation, upstream
  API/transport/timeout failures, cancellation, and persistence failures.
