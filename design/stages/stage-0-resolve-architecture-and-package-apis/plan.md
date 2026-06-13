# Stage 0: Resolve Architecture and Package APIs

## Goal

Remove all architecture and dependency API uncertainty before scaffolding or
implementation begins.

## Dependencies

None. This stage blocks every later stage.

## Implementation Status

**Blocked.** Package discovery is substantially complete, but the stage exit
criteria have not passed:

- `docs:company-docs` returns `insufficient_evidence` for the API architecture
  standard, so the solution and project map cannot be finalized.

Formula.SimpleRepo 2.8.1 compatibility with a `net10.0` consumer has been
verified by restore and build. Its SQLite dialect, transient registration
lifetime, schema initialization approach, and atomic upsert approach are also
resolved and recorded in [evidence.md](evidence.md). SimpleRepo is not a
blocker.

Verified contracts and citation URIs are recorded in
[evidence.md](evidence.md). Do not start Stage 1 until the company architecture
blocker is resolved and this status is changed to complete.

## Work

- [ ] Query `docs:company-docs` for the current .NET API architecture standard
  and receive usable evidence. Queries currently return
  `insufficient_evidence`.
- [ ] Record the required solution name, project names, directory structure,
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

- [ ] Documented company architecture contract.
- [x] Documented package API and SQLite implementation contracts.
- [ ] Final project and namespace map for the `City` API.
- [x] Confirmed package versions and sources.

## Exit Criteria

- DevContext returns usable evidence rather than `not_found`, `not_ready`, or
  `insufficient_evidence`.
- No project structure or third-party API remains inferred.
- The implementation can compile against every selected package contract.
