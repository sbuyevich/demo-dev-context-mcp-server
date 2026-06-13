# Stage 4: Implement HTTP Endpoints

## Goal

Expose the four specified JSON endpoints and map application outcomes to the
required HTTP contracts.

## Dependencies

- Stage 2 and Stage 3 exit criteria must pass.

## Work

- Map `GET /city` to `ICityService.GetCityNames()` without reordering.
- Map `GET /city/usa` to `IUsaCityService.GetCityNames()` without reordering.
- Map `GET /city/{cityName}/location` to the shared geocoding service.
- Map `GET /city/{cityName}/population` to the same service.
- Define separate location and population response DTOs.
- Return typed JSON results for success.
- Return Problem Details for `404`, `502`, and `500` responses with a trace ID
  and no internal exception details.
- Ensure the static `/usa` route cannot be captured as a city name.
- Add endpoint names and OpenAPI response metadata.

## Deliverables

- All four public endpoints.
- Stable success DTOs and error contracts.
- Centralized exception handling for internal failures.

## Exit Criteria

- Every endpoint returns the status, content type, and JSON shape documented in
  `design/spec.md`.
- URL-encoded and mixed-case city names resolve correctly.
- Missing population returns `404` while cached location remains available.
- Upstream failure on a cache miss returns `502`.
- Internal persistence failures return sanitized `500` Problem Details.
