# MailLoadTester — MASTER FIX PLAN

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> This document consolidates the repair ledger and audit-progress documents previously maintained in the parallel `load` repository. Historical copies remain secondary references only.

## Rules

- Verify every item against the current `load2/main` source before changing code.
- A finding is not FIXED because a comment claims it is fixed; source + callers + regression test must support the status.
- Do not copy implementation from `load` or `Load-tester-` merely because it exists there. Reconcile against `load2/main` first.
- Keep network/SMTP transport separate from message/payload generation.
- Preserve asynchronous/thread-safe behavior, `SemaphoreSlim`, `Volatile`, and `CancellationToken` semantics.
- Security/load-testing behavior remains explicitly authorized and bounded.
- Build/test results must not be claimed unless actually executed or supported by CI evidence.

## Current open repair backlog

| ID | Severity | Area | Status | Next action |
|---|---|---|---|---|
| BUG-001 | High | SmtpTestRunner / global concurrency | OPEN | Replace unbounded per-message task creation with bounded worker/channel model; add regression test. |
| BUG-002 | High | SmtpTestRunner / auto-restart | OPEN | Resume only unfinished message indexes, or explicitly define whole-test rerun semantics; add duplicate-delivery regression test. |
| BUG-003 | Medium | SmartPaceController / first send | OPEN | Verify first reservation is immediate; add timing/regression test. |
| BUG-004 | Medium | ProxyRotator / random selection | OPEN | Select only currently unblocked endpoints without replacement; add repeated-selection test. |
| BUG-005 | Medium | IpV4Rotator / /31 | OPEN | Handle `/31` as two addresses and `/32` as one; add regression tests. |
| BUG-006 | Medium | Repository integrity | OPEN-preventive | Protect canonical `src/`, `tests/`, `installer/` layout and prevent Git-internal artifacts from returning. |
| BUG-007 | High | Retry pacing | OPEN | Re-enter global pacing before each retry; add concurrent retry timing test. |
| BUG-008 | High | Direct MX multi-domain routing | OPEN | Resolve/cache MX per recipient domain or reject mixed-domain input explicitly; add multi-domain regression test. |

## Completed deep-audit findings

The previous deep audit recorded 10 concrete findings as fixed. These are historical completed items and must remain traceable, but should not be reimplemented blindly:

1. `EmlTemplateParser`: avoid duplicate `HtmlBody` read; dispose `MimeMessage`; improve `MemoryStream` capacity handling.
2. `RandomTestData.CreatePaddedPng`: protect small buffer sizes; regression coverage added.
3. `AdaptiveConcurrencyLimiter`: fix grant/cancellation race that could leak permits; stress test added.
4. `SmartPaceController`: cache parsed warm-up phases.
5. `IpV4Rotator`: enforce CIDR expansion limit during enumeration to prevent explosive allocation/OOM risk.
6. `IpBindingHelper`: derive socket address family from the actual source endpoint when `IpVersion=Any` and a source IP is supplied.
7. `MxResolver`: distinguish confirmed negative DNS results from transient query failure; use short failure caching instead of poisoning the full negative cache period.
8. `CircuitBreaker`: preserve the previously added sticky `EverOpened` behavior.
9. `SmtpConnectionPool`: lifecycle/permit/shutdown race fixes, including in-flight TLS certificate protection.
10. `SmtpSessionLogger`: preserve buffered diagnostics when disk write fails, with a bounded retry buffer.

These historical fixes are documented in the source audit material and should be re-verified when touching the affected components.

## Round 4 / next audit backlog

### Completed in the previous round

- Full `SmtpConnectionPool` pass: no additional confirmed race found.
- Silent `catch {}` occurrences classified; idle reconnect logging improved.
- `SmtpSessionLogger` failed-write buffer handling fixed.
- `MainForm` lifecycle shutdown issue fixed with cancellation/waiting and defensive UI invocation checks.
- Integer/size overflow paths in attachment planning checked; no confirmed issue.

### Explicitly NOT VERIFIED yet

1. **AUTH secret redaction end-to-end test** — fake SMTP AUTH LOGIN/PLAIN flow must prove neither plaintext password nor its base64 representation reaches protocol/session logs.
2. **Path validation audit** — EML templates, attachments, inline attachments, profiles, exports and webhook-related paths; examine UNC, junction/symlink and invalid-access behavior where applicable.
3. **Cross-component concurrency audit** — `CircuitBreaker` ↔ `RateLimiter` ↔ `SmartPaceController` ↔ `AdaptiveConcurrencyLimiter` ↔ `PerRecipientLimiter` ↔ `SmtpConnectionPool` as one state machine.
4. **Cancellation audit of Core** — systematically inspect awaits and verify correct propagation/handling of `CancellationToken`.
5. **Build/test verification** — run from a clean checkout:

```text
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

## Recommended execution order

### Phase A — correctness blockers
1. Run clean `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release` on Windows/CI.
2. Fix BUG-001 global worker bound.
3. Fix BUG-002 auto-restart semantics.
4. Fix BUG-007 retry pacing.
5. Fix BUG-008 direct-MX multi-domain routing.

### Phase B — networking/configuration correctness
6. Fix BUG-004 proxy selection.
7. Fix BUG-005 `/31` handling.
8. Verify BUG-003 first pacing slot.

### Phase C — security and robustness verification
9. AUTH secret redaction integration test.
10. Full cancellation audit.
11. Cross-component concurrency/state-machine audit.
12. Path/UNC/symlink audit.

### Phase D — final release gate
13. Full test suite.
14. Release build.
15. Smoke tests:
   - Start → Stop during SMTP connect
   - Direct MX, one domain
   - Direct MX, multiple domains
   - partially blocked proxy list
   - `/31` and `/32` source rotation
   - auto-restart after partial failure
   - retry pacing with `IntervalMs > 0`
   - high `MessageCount` with low `MaxConcurrency` in dry-run
   - profile round-trip
16. Update `BUGS-AUDIT.md` and this plan only from verified evidence.

## Status semantics

- `OPEN` = confirmed problem not yet fixed/verified.
- `OPEN pending verification` = plausible finding requiring targeted runtime/test evidence.
- `FIXED` = source and regression test changed and verified.
- `PASS` = audited with no confirmed defect for the stated scope.
- `NOT VERIFIED` = not yet checked; never treat as PASS.

## Provenance

Consolidated from the audit/repair records found in the parallel `load` repository, including `BUGS-AUDIT.md`, `AUDIT-FIXES-DEEP-PHASE1-3.md`, and `AUDIT-ROUND4-PROGRESS.md`, then reconciled with the current `load2/main` audit ledger. `load2/main` remains authoritative.
