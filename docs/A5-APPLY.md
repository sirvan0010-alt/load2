# A5 apply instructions — SUPERSEDED

**Current status:** A5 is **FIXED / CLOSED** as part of TRACK A. Do not execute the instructions in this file against the current `main` branch.

The historical instructions are retained only as an audit trail of the earlier A5 implementation step.

## Current authority

- `docs/IMPLEMENTATION-BACKLOG.md` → A5 ✅ FIXED
- `docs/A8-BASELINE.md` → A5 ✅ FIXED
- `docs/AI-GUIDE.md` → TRACK A CLOSED
- GitHub Issue #4 → TRACK A CLOSED

## Current implementation contract

`SmtpOutcome.cs` / `SmtpOutcomeClassifier` / `SmtpOutcomeCounters` are already part of the current baseline. Retry decisions and endpoint-health classification use the unified outcome taxonomy, and `OutcomeCounts` is part of run observability/reporting.

Do **not** repeat the old `Models.cs` or `SmtpTestRunner.cs` edits from this document.

For a new outcome-classification change, create a separate post-baseline task only when a concrete source/test regression is demonstrated.
