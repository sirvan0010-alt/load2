# MailLoadTester — MASTER AUDIT REGISTER

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> Audit-first workflow: inspect the complete canonical codebase, register findings, then perform grouped repairs and final verification. `load` is secondary; `Load-tester-` is legacy.

## Audit method

Every finding must contain:
- exact component/file/method;
- evidence from current `load2/main`;
- severity and impact;
- proposed remediation;
- regression-test requirement;
- status (`OPEN`, `PENDING VERIFICATION`, `PASS`, `FIXED`, `NOT VERIFIED`).

Do not mark an item fixed from comments or historical documents alone.

## Phase 1 — Correctness / execution model

### Confirmed

- **BUG-001 — HIGH — global MaxConcurrency is not a strict execution bound**
  - File: `src/MailLoadTester.Core/SmtpTestRunner.cs`
  - Evidence: each batch creates one async task per message with `Enumerable.Range(...).Select(async i => ...)`, while the SMTP pool semaphore only bounds leased SMTP clients. MIME generation, pacing, dry-run work and queued task state are therefore not globally bounded by `MaxConcurrency`.
  - Impact: high `MessageCount` can create excessive task/state/CPU/memory pressure and the configured concurrency does not represent a true worker bound.
  - Remediation: bounded worker/channel pipeline while preserving persistent SMTP sessions, adaptive concurrency, pacing and cancellation.
  - Regression: high message count + low concurrency; assert active message pipeline never exceeds configured worker bound.
  - Status: OPEN.

- **BUG-002 — HIGH — auto-restart can duplicate successful deliveries**
  - File: `src/MailLoadTester.Core/SmtpTestRunner.cs`
  - Evidence: `RunAsync` invokes `RunSingleAsync(current, ...)` again for the complete `MessageCount` after a mostly-failed run; no durable per-message completion ledger is used.
  - Impact: already delivered messages may be delivered again and aggregate statistics count attempts rather than unique message indexes.
  - Remediation: resume only unfinished indexes, or make whole-run retry an explicit non-delivery-safe mode with clearly separated accounting. Prefer unfinished-index resume for normal operation.
  - Regression: partial failure with successful indexes; verify successful indexes are not sent twice.
  - Status: OPEN.

- **BUG-007 — HIGH — retry delay bypasses global pacing**
  - File: `src/MailLoadTester.Core/SmtpTestRunner.cs`
  - Evidence: retry path uses `Task.Delay(GetRetryDelay(attempt), ct)` without a new `SmartPaceController.WaitBeforeSendAsync` reservation.
  - Impact: retries can burst independently of configured global spacing.
  - Remediation: every actual send attempt, including retry, must acquire the global pacing slot exactly once.
  - Regression: concurrent transient failures with non-zero interval; verify retry attempts obey pacing.
  - Status: OPEN.

### Existing findings carried forward

- **BUG-003 — MEDIUM** first pacing reservation timing: targeted runtime verification required. Status OPEN/pending verification.
- **BUG-008 — HIGH** direct-MX mode resolves only the first recipient domain and can route mixed-domain recipients incorrectly. Status OPEN.

## Phase 2 — Networking / configuration correctness

- **BUG-004 — MEDIUM — ProxyRotator can repeatedly sample blocked endpoints.** Status OPEN.
- **BUG-005 — MEDIUM — IPv4 `/31` can lose one usable address.** Status OPEN.
- **BUG-008 — HIGH — direct-MX multi-domain routing.** Status OPEN.
- **NET-AUDIT-001 — NOT VERIFIED — source IP/socket family/rotation interactions.** Recheck `IpBindingHelper`, IPv4/IPv6 rotation and proxy combinations as one path.
- **NET-AUDIT-002 — NOT VERIFIED — TLS configuration matrix.** Verify plaintext/STARTTLS/implicit TLS, certificate validation, client certificate and cancellation/error transitions.

## Phase 3 — Security / robustness

- **SEC-AUDIT-001 — NOT VERIFIED — AUTH secret redaction end-to-end.** Fake SMTP AUTH LOGIN/PLAIN integration test must prove neither plaintext nor base64 credentials enter protocol/session logs.
- **SEC-AUDIT-002 — NOT VERIFIED — path validation.** Audit EML templates, attachments, inline attachments, profiles, exports and webhook paths, including UNC/junction/symlink behavior where relevant.
- **SEC-AUDIT-003 — NOT VERIFIED — configuration secret sources.** Verify credentials/passwords are supplied through configuration/environment mechanisms and never hardcoded.
- **SEC-AUDIT-004 — NOT VERIFIED — `--unauthorized` safety gate.** Verify CLI behavior requires the explicit authorization flag for network load operations and that dry-run remains non-networking.

## Phase 4 — Concurrency / state machines

- **CONC-AUDIT-001 — NOT VERIFIED — cross-component state machine.** Audit `CircuitBreaker` ↔ `RateLimiter` ↔ `SmartPaceController` ↔ `AdaptiveConcurrencyLimiter` ↔ `PerRecipientLimiter` ↔ `SmtpConnectionPool`.
- **CONC-AUDIT-002 — NOT VERIFIED — cancellation propagation.** Inspect every Core async path for correct token propagation and cleanup.
- Historical `SmtpConnectionPool` and `AdaptiveConcurrencyLimiter` passes found no additional confirmed race, but they must be regression-tested after BUG-001 changes.

## Phase 5 — Performance / resource lifecycle

- **PERF-AUDIT-001 — OPEN — unbounded per-message task creation** overlaps BUG-001 and is tracked there.
- **PERF-AUDIT-002 — NOT VERIFIED — allocation profile under large MessageCount.** Inspect MIME/message generation, attachment copying, progress snapshots and observed-response collection.
- **PERF-AUDIT-003 — NOT VERIFIED — session logging throughput/backpressure.** Verify logging cannot become the dominant producer-side bottleneck or unbounded buffer.

## Phase 6 — GUI / UX / operability

- Historical `MainForm` shutdown/lifecycle issue is recorded as fixed in prior audit material; reverify after runner changes.
- **GUI-AUDIT-001 — NOT VERIFIED — progress semantics after bounded-worker refactor.** Ensure Sent/Failed/ETA and worker information remain coherent.
- **GUI-AUDIT-002 — NOT VERIFIED — Stop behavior.** Verify UI returns promptly and no background send continues after shutdown.
- **GUI-AUDIT-003 — NOT VERIFIED — dashboard lifecycle and port collision behavior.** Existing behavior intentionally treats dashboard startup as non-fatal; verify cleanup.

## Phase 7 — Architecture / maintainability / feature opportunities

- Preserve transport/message separation and `IMailPayloadPlugin` extension points.
- **ARCH-AUDIT-001 — NOT VERIFIED — plugin discovery/lifecycle/thread safety.** Audit plugin interfaces, loading, invocation and exception isolation.
- **ARCH-AUDIT-002 — NOT VERIFIED — options/configuration cohesion.** Identify duplicated validation/defaulting and unsafe implicit defaults.
- **ARCH-AUDIT-003 — NOT VERIFIED — test architecture.** Map production components to unit/integration/stress coverage and identify untested state transitions.

Feature candidates are tracked separately in `FEATURE-BACKLOG.md`; they must not be confused with existing capabilities.

## Phase 8 — Final verification / release gate

Required evidence before release status:

1. clean `dotnet restore`;
2. `dotnet build -c Release`;
3. `dotnet test -c Release`;
4. targeted regression suite for every fixed BUG;
5. smoke tests for cancellation, direct MX, proxy rotation, `/31`/`/32`, retries, auto-restart and profile round-trip;
6. verify `--unauthorized` gate;
7. verify no credentials/secrets are exposed in logs;
8. review final git tree for repository integrity.

No PASS claim without execution evidence or trustworthy CI evidence.

## Current audit rule

The audit proceeds across all eight phases before the final repair/verification cycle. New bugs are registered here as they are discovered; new capabilities are registered in `FEATURE-BACKLOG.md`. Code changes should be grouped after the audit so interconnected fixes can be designed together rather than causing serial regressions.
