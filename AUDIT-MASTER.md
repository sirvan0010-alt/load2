# MailLoadTester — MASTER AUDIT REGISTER

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> Audit-first workflow: phases 1–7 are statically audited against current `main`; phase 8 is reserved for execution/build/test verification.

## Audit method

For each component we checked entry points/callers, async and cancellation flow, shared state and synchronization, resource ownership, boundary values, retry/pacing interactions, security-sensitive data flow, configuration validation, GUI lifecycle, and regression-test coverage. Confirmed defects are recorded in `BUGS-AUDIT.md`; unverified suspicions remain explicitly marked `NOT VERIFIED`.

## Phase 1 — Correctness / execution model — AUDITED

Confirmed:
- **BUG-001 HIGH:** `SmtpTestRunner` creates per-message async tasks rather than a strict global worker bound; `MaxConcurrency` therefore does not bound the complete message pipeline.
- **BUG-002 HIGH:** auto-restart repeats the complete message set and can duplicate successful deliveries because no per-message delivery ledger exists.
- **BUG-007 HIGH:** retry delay does not reserve a new global `SmartPaceController` slot.
- **BUG-008:** runner-level Direct MX logic resolves the first recipient domain only. Current `Validation.Validate()` rejects mixed-domain Direct MX configurations, so the unsafe mixed-domain path is currently blocked at the public validation boundary; structural hardening remains required.
- **BUG-003 MEDIUM:** first global pacing reservation is statically confirmed to wait one full interval when the schedule is initially empty.
- **BUG-009 HIGH:** global pacing is reserved before adaptive/pool admission, so it does not strictly guarantee spacing between actual `SendAsync` operations under downstream contention.

## Phase 2 — Networking / configuration correctness — AUDITED

Checked SMTP pool lifecycle, proxy selection/ban state, IPv4/IPv6 binding/rotation, MX resolution, TLS mode validation, source-IP interactions and idle health checks.

Confirmed/carry-forward:
- **BUG-004 MEDIUM:** blocked proxy endpoints can remain eligible for random selection.
- **BUG-005 MEDIUM:** IPv4 `/31` handling requires RFC 3021 semantics.
- Direct-MX mixed-domain configuration is currently rejected by validation; see BUG-008 note above.

Not runtime-verified: full TLS/connect/certificate matrix and combined source-IP/proxy/socket-family matrix.

## Phase 3 — Security / robustness — AUDITED STATICALLY

Checked authentication configuration, custom-header validation, EML/attachment limits, path traversal checks, session logging, webhook behavior and safety-gate references.

Important verification blockers:
- **SEC-AUDIT-001 NOT VERIFIED:** end-to-end AUTH secret redaction. `SessionProtocolLogger` exposes raw `LogClient` bytes and declares `AuthenticationSecretDetector`, but source inspection alone does not prove that the detector is wired and masks AUTH traffic for this custom logger. This requires a fake-SMTP integration test; do not mark as a confirmed leak without that evidence.
- **SEC-AUDIT-002 NOT VERIFIED:** complete UNC/junction/symlink/path-boundary audit across every file-producing/reading feature.
- **SEC-AUDIT-003 NOT VERIFIED:** complete configuration/environment provenance audit for all secrets.
- **SEC-AUDIT-004 NOT VERIFIED:** end-to-end `--unauthorized` enforcement and DryRun non-networking behavior.

Positive evidence: custom header values reject CR/LF; validation rejects `..` segments in EML and session-log paths; attachment existence and message-size limits are validated.

## Phase 4 — Concurrency / state machines — AUDITED STATICALLY

Checked the interaction model among `CircuitBreaker`, `RateLimiter`, `SmartPaceController`, `AdaptiveConcurrencyLimiter`, `PerRecipientLimiter` and `SmtpConnectionPool`, including permit ownership and shutdown/cancellation transitions. Individual limiter/pool inspections found no additional confirmed primitive race.

Composition defect confirmed:
- **BUG-009 HIGH:** pacing slot reservation occurs before adaptive concurrency and SMTP pool admission. Internal pacing state is synchronized, but the end-to-end execution path does not preserve the promised actual-send spacing when later admission is delayed.

Not runtime-verified: full cross-component stress/cancellation matrix.

## Phase 5 — Performance / resource lifecycle — AUDITED STATICALLY

Confirmed primary performance defect:
- **BUG-001 / PERF-AUDIT-001:** task-per-message scheduling can create excessive task/state pressure for high `MessageCount`.

Additional correctness/performance interaction:
- **BUG-009:** unnecessary queued pacing reservations plus downstream admission can create schedule churn and out-of-order actual sends.

Reviewed attachment preloading/safety, progress throttling, session-log buffering, connection reuse and dry-run paths. No additional confirmed defect was established from static inspection.

Runtime allocation/throughput profiling remains unverified.

## Phase 6 — GUI / UX / operability — AUDITED STATICALLY

The historical `MainForm` shutdown/lifecycle defect is already fixed in prior audit work: closing the form cancels the run and waits for completion before final close, with defensive UI invocation checks.

Current remaining verification:
- GUI progress/ETA semantics after the future worker refactor;
- Stop behavior under SMTP connect/retry;
- dashboard cleanup and port collision behavior.

No new confirmed GUI defect was established in this pass.

## Phase 7 — Architecture / maintainability / feature opportunities — AUDITED

Architecture review confirms the Core is organized around SMTP transport/session infrastructure plus separate message/payload helpers, and the existing test project provides focused unit coverage for several core components.

Open architecture verification items:
- **ARCH-AUDIT-001:** plugin discovery/lifecycle/thread safety requires focused inspection of the actual plugin implementation/callers.
- **ARCH-AUDIT-002:** options/default/validation cohesion needs consolidation review.
- **ARCH-AUDIT-003:** production-to-test coverage map needs completion.

Feature opportunities remain separate in `FEATURE-BACKLOG.md`. Safe candidates include bounded worker execution, per-message delivery ledger, SMTP diagnostics, fake SMTP integration harness, DNS policy diagnostics and configuration preflight. Uncontrolled flooding/spam/DoS/DDoS behavior is outside the implementation target.

## Phase 1–7 conclusion

**TARGETED DEEP AUDIT COMPLETE.** The audit found two additional source-confirmed defects beyond the previous ledger: **BUG-003** (first pacing slot) and **BUG-009** (pacing reservation does not compose correctly with downstream admission). The confirmed ledger is now BUG-001 through BUG-009.

The audit should now stop. Next is **repair of the registered defects**, followed by Phase 8 verification. No PASS/FIXED claim without execution evidence or trustworthy CI evidence.

## Phase 8 — Final verification — NOT STARTED

Required evidence:
1. `dotnet restore`
2. `dotnet build -c Release`
3. `dotnet test -c Release`
4. targeted regression tests for each fix
5. cancellation/start-stop smoke tests
6. Direct MX tests
7. proxy partial-ban tests
8. IPv4 `/31` and `/32`
9. retry pacing
10. auto-restart duplicate-delivery protection
11. high-message-count/low-concurrency DryRun
12. AUTH redaction integration test
13. path/security tests
14. profile round-trip
15. final repository-integrity review
