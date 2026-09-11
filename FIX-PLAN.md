# MailLoadTester — MASTER FIX PLAN

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> This is the master execution plan. The workflow is now audit-first: inspect all eight phases, register all confirmed defects and feature opportunities, then repair interconnected items as groups and finish with a release verification gate.

## Repository authority

- `sirvan0010-alt/load2` / `main` = sole source of truth.
- `sirvan0010-alt/load` = secondary/parallel copy only.
- `sirvan0010-alt/Load-tester-` = legacy; do not use for new implementation.

## Rules

- Verify every item against the current `load2/main` source before changing code.
- A finding is not FIXED because a comment claims it is fixed; source + callers + regression test + verification evidence must support the status.
- Do not copy implementation from secondary/legacy repositories merely because it exists there.
- Keep network/SMTP transport separate from message/payload generation.
- Preserve asynchronous/thread-safe behavior, `SemaphoreSlim`, `Volatile`, and `CancellationToken` semantics.
- Security/load-testing behavior remains explicitly authorized and bounded.
- Build/test results must not be claimed unless actually executed or supported by CI evidence.
- New feature ideas are recorded separately from bugs and are not treated as implemented.

## Master audit register

`AUDIT-MASTER.md` is the canonical phase-by-phase audit register.

`BUGS-AUDIT.md` is the confirmed defect ledger.

`FEATURE-BACKLOG.md` contains proposed improvements.

## Phase 1 — Correctness / execution model

Audit first:
- full message scheduling and worker lifecycle;
- MaxConcurrency semantics;
- retries and delivery accounting;
- auto-restart semantics;
- pacing and retry pacing;
- direct-MX recipient routing;
- result/statistics correctness;
- exception classification.

Known findings: BUG-001, BUG-002, BUG-007, BUG-008.

Repair after phase-wide audit: bounded worker/channel execution, unique-message accounting, retry pacing and per-domain MX routing.

## Phase 2 — Networking / configuration correctness

Audit:
- SMTP connection pool;
- proxy rotation/ban state;
- IPv4/IPv6 binding and rotation;
- DNS/MX resolution;
- TLS modes and certificate validation;
- source-IP and proxy interactions;
- connection reuse/health checks.

Known findings: BUG-004, BUG-005.

Pending verification: BUG-003 and TLS/network interaction matrix.

## Phase 3 — Security / robustness

Audit:
- AUTH secret redaction;
- credential/configuration sources;
- `--unauthorized` safety gate;
- path traversal/UNC/junction/symlink handling;
- EML and attachment quotas;
- webhook/export paths;
- logging of sensitive protocol data;
- cancellation during network and filesystem operations.

Required evidence: fake-SMTP AUTH integration test and targeted path/security tests.

## Phase 4 — Concurrency / state machines

Audit the combined state machine:

`CircuitBreaker ↔ RateLimiter ↔ SmartPaceController ↔ AdaptiveConcurrencyLimiter ↔ PerRecipientLimiter ↔ SmtpConnectionPool`

Check permit ownership, reservation rollback, cancellation, shutdown, fairness, deadlocks, starvation and duplicate release. Re-run concurrency tests after any runner/pool changes.

## Phase 5 — Performance / resource lifecycle

Audit:
- task allocation under large MessageCount;
- MIME allocation and disposal;
- attachment memory/file handles;
- progress-report pressure;
- observed-response collection;
- session logging backpressure;
- connection reuse;
- CPU and memory behavior in dry-run and real SMTP modes.

Optimize only after correctness semantics are established.

## Phase 6 — GUI / UX / operability

Audit:
- Start/Stop lifecycle;
- UI cancellation and shutdown;
- progress/ETA/statistics consistency;
- dashboard lifecycle;
- profile handling;
- error presentation;
- safe defaults and minimal configuration.

Historical MainForm shutdown fix must be reverified after runner changes.

## Phase 7 — Architecture / maintainability / feature opportunities

Audit:
- `IMailPayloadPlugin` lifecycle/discovery/thread safety;
- transport vs payload separation;
- options/default/validation duplication;
- test coverage mapping;
- observability model;
- opportunities for automation and diagnostics.

Proposals go to `FEATURE-BACKLOG.md`. Defensive diagnostics such as SPF/DKIM/DMARC, TLS capability checks and bounded open-relay verification may be considered. Flooding, mailbombing, spam, DoS/DDoS behavior is outside the implementation target.

## Phase 8 — Final verification / release gate

Only after phases 1–7 and grouped repairs:

1. clean `dotnet restore`;
2. `dotnet build -c Release`;
3. `dotnet test -c Release`;
4. targeted regression tests for every fixed finding;
5. cancellation/start-stop smoke tests;
6. direct-MX one-domain and multi-domain tests;
7. proxy partial-ban tests;
8. IPv4 `/31` and `/32` tests;
9. retry pacing tests;
10. auto-restart partial-failure/duplicate-delivery test;
11. high MessageCount + low MaxConcurrency dry-run test;
12. AUTH log-redaction test;
13. path/security tests;
14. profile round-trip;
15. final repository-tree/integrity review.

## Current status

- Phase 1 audit: IN PROGRESS; BUG-001/002/007/008 confirmed from current source.
- Phase 2 audit: IN PROGRESS; BUG-004/005 carried forward; broader network/TLS matrix pending.
- Phase 3: NOT VERIFIED.
- Phase 4: partial historical review completed; full cross-component audit pending.
- Phase 5: NOT VERIFIED.
- Phase 6: historical lifecycle fix exists; post-refactor verification pending.
- Phase 7: feature backlog initialized; architecture audit pending.
- Phase 8: BLOCKED until phases 1–7 and repair verification are complete.

## Status semantics

- `OPEN` = confirmed problem not yet fixed/verified.
- `OPEN pending verification` = plausible finding requiring targeted runtime/test evidence.
- `FIXED` = source and regression test changed and verified.
- `PASS` = audited with no confirmed defect for the stated scope.
- `NOT VERIFIED` = not yet checked; never treat as PASS.

## Provenance

This plan consolidates the previous audit/repair records while treating the current `load2/main` tree as authoritative. Historical audit documents remain evidence/history, not implementation authority.
