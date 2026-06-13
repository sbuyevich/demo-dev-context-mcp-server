# Stage 3: Implement City Resolution and Geocoding

## Goal

Implement the shared cache-aside workflow that resolves a supported city and
supplies both location and population data.

## Dependencies

- Stage 1 exit criteria must pass.
- The Stage 2 repository contract must be agreed before work begins.
- Stage 2 integration must pass before this stage can complete.

## Work

- Define `ICityGeocodingService` with one operation that supplies the shared
  geocoding record used by both detail endpoints.
- Match route city names against `ICityService.GetCityNames()` after trimming,
  using `StringComparer.OrdinalIgnoreCase`.
- Preserve the package-provided spelling as the canonical display name.
- Normalize cache keys with `Trim().ToUpperInvariant()`.
- Return a not-found outcome before cache or upstream access for unknown
  cities.
- On a cache miss, call the verified `IOpenMeteoClient` geocoding operation
  with the canonical name and request cancellation token.
- Select the first upstream result whose city name exactly matches using
  `OrdinalIgnoreCase`.
- Map and persist one record containing coordinates and population.
- Map upstream transport, timeout, or unsuccessful-response failures to a
  service-unavailable outcome without swallowing request cancellation.

## Deliverables

- `CityGeocodingService` and explicit service outcome model.
- Test doubles for city services, Open-Meteo, cache repository, and time.
- Unit tests for normalization, result selection, cache-aside behavior, and
  failures.

## Exit Criteria

- A cache hit never calls Open-Meteo.
- A cache miss calls Open-Meteo once and persists the selected result.
- Unknown cities call neither the cache nor Open-Meteo.
- Empty or nonmatching upstream results produce a not-found outcome.
- Upstream failures and request cancellation remain distinct.
- One service result supplies both location and population data.
