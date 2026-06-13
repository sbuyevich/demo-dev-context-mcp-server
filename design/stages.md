# City Implementation Stages

Implementation follows these stages in order:

1. [Stage 0: Resolve Architecture and Package APIs (blocked)](stages/stage-0-resolve-architecture-and-package-apis/plan.md)
2. [Stage 1: Scaffold the City Solution](stages/stage-1-scaffold-the-city-solution/plan.md)
3. [Stage 2: Implement the SQLite Cache](stages/stage-2-implement-the-sqlite-cache/plan.md)
4. [Stage 3: Implement City Resolution and Geocoding](stages/stage-3-implement-city-resolution-and-geocoding/plan.md)
5. [Stage 4: Implement HTTP Endpoints](stages/stage-4-implement-http-endpoints/plan.md)
6. [Stage 5: Complete Integration Coverage](stages/stage-5-complete-integration-coverage/plan.md)
7. [Stage 6: Final Verification and Handoff](stages/stage-6-final-verification-and-handoff/plan.md)

Stage 0 blocks all implementation. Each later stage requires the preceding
stage's exit criteria to pass.

Stages 2 and 3 may be developed in parallel only after their shared repository
and service interfaces are agreed. Stage 3 cannot complete until the SQLite
repository is integrated.
