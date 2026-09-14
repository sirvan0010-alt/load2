# A4 apply instructions — SUPERSEDED

**Current status:** A4 is **FIXED / CLOSED** as part of TRACK A. Do not execute the instructions in this file against the current `main` branch.

The historical instructions below are retained only as an audit trail of the earlier A4 implementation step. The current implementation, tests and final status are authoritative.

## Current authority

- `docs/IMPLEMENTATION-BACKLOG.md` → A4 ✅ FIXED
- `docs/A8-BASELINE.md` → A4 ✅ FIXED
- `docs/AI-GUIDE.md` → TRACK A CLOSED
- GitHub Issue #4 → TRACK A CLOSED

## Important

Do **not** run the old `git apply docs/a4-runner.diff` procedure or recreate the old `Models.cs` / `SmtpTestRunner.cs` edits. Those changes are already represented by the current `main` implementation.

The retry histogram / AutoRestart work (`5e0689f`, `f2108f8`) is existing work and must not be duplicated.

For new retry changes, create a separate post-baseline task only when a concrete source/test regression is demonstrated.
