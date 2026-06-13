# Stage 0: Resolve Architecture and Package APIs

## Goal

Remove all architecture and dependency API uncertainty before scaffolding or
implementation begins.

## Dependencies

None. This stage blocks every later stage.

## Implementation Status

**Complete.** DevContext returned the company API architecture standard at
`docs://company-docs/api.architecture.md`, and all required package contracts
are resolved.

Formula.SimpleRepo 2.8.1 compatibility with a `net10.0` consumer has been
verified by restore and build. Its SQLite dialect, transient registration
lifetime, schema initialization approach, and atomic upsert approach are also
resolved and recorded in [evidence.md](evidence.md). SimpleRepo is not a
blocker.

Verified contracts, project structure, and citation URIs are recorded in
[evidence.md](evidence.md). Stage 1 is unblocked.

## Work

- [x] Query `docs:company-docs` for the current .NET API architecture standard.
- [x] Record the required solution name, project names, directory structure,
  project references, namespaces, and test-project conventions.
- [x] Verify the exact dependency-injection extension and public APIs for:
  - QA `Demo.Cities` 1.1.0;
  - `OpenMeteo.Api.Client` 1.0.0; and
  - `Formula.SimpleRepo` 2.8.1.
- [x] Confirm `Formula.SimpleRepo` SQLite connection, query, insert/upsert,
  transaction, lifetime, and schema-initialization APIs.
- [x] Add all available evidence citation URIs to `design/spec.md` and
  [evidence.md](evidence.md).

## Deliverables

- [x] Documented company architecture contract.
- [x] Documented package API and SQLite implementation contracts.
- [x] Final project and namespace map for the `City` API.
- [x] Confirmed package versions and sources.

## Exit Criteria

- DevContext returns usable evidence rather than `not_found`, `not_ready`, or
  `insufficient_evidence`.
- No project structure or third-party API remains inferred.
- The implementation can compile against every selected package contract.
