# Stage 6: Final Verification and Handoff

## Goal

Demonstrate that the implementation satisfies the specification and can be
run, reviewed, and deployed without undocumented steps.

## Dependencies

- Stage 5 exit criteria must pass.

## Work

- Run restore, build, and all automated tests from a clean working tree.
- Start the API with a fresh SQLite database and exercise all four routes.
- Confirm the database is created outside public content and contains one row
  per normalized city.
- Review logs and Problem Details responses for leaked connection strings,
  upstream bodies, exception messages, or stack traces.
- Compare implementation behavior with every acceptance item in
  `design/spec.md`.
- Update implementation documentation with startup instructions,
  configuration, verified package citations, and known ambiguity for duplicate
  city names.

## Deliverables

- Passing build and test evidence.
- Completed acceptance checklist.
- Operational README or equivalent company-standard documentation.

## Exit Criteria

- All specification acceptance criteria pass.
- No unresolved architecture or package API verification gates remain.
- The API can be configured and run without undocumented manual steps.
- The implementation is ready for review and deployment through the normal
  company process.
