# BUGS-AUDIT — MailLoadTester

## Audit baseline

- Repository: `sirvan0010-alt/load2`
- Branch: `main`
- Audit baseline commit: `811d424497435be94bbae34df96c7cfbf8eec453`
- Latest audit-ledger commit: maintained on `main`
- Audit method: static source inspection against the actual repository tree and code; no claim of successful local `dotnet test`/`dotnet build` unless CI evidence exists.
- `load2` structure is confirmed as a normal solution layout: `src/MailLoadTester.Core`, `src/MailLoadTester.Gui`, `tests/MailLoadTester.Tests`, `installer`, and root `MailLoadTester.sln`.
- `Load-tester-` remains the historical/source-of-truth reference for reconciliation; `load2` is now the correctly structured audit target.

## Confirmed findings

### BUG-001 — HIGH — MaxConcurrency is not a global execution bound
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

`RunSingleAsync` creates one asynchronous task for every message in each batch. When adaptive concurrency is disabled, there is no global worker limiter around the full message pipeline. The SMTP pool limits leased SMTP clients, but it does not limit queued tasks, MIME generation, pacing, dry-run work, or other per-message processing. In particular, dry-run execution can run all batch tasks concurrently despite `MaxConcurrency`.

**Impact:** high `MessageCount` can create excessive task/state overhead and CPU/memory pressure; `MaxConcurrency` does not mean a strict global worker limit.

**Status:** OPEN

### BUG-002 — HIGH — Auto-restart can duplicate successful deliveries
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

When `AutoRestartOnFailure` is enabled and a run has more failures than successes, `RunAsync` starts the whole message set again with reduced concurrency. Messages already accepted successfully by the SMTP server can therefore be sent again. The returned aggregate counts both attempts, but there is no per-message durable delivery ledger that prevents duplicates.

**Impact:** duplicate deliveries and ambiguous load-test results.

**Status:** OPEN

### BUG-003 — MEDIUM — First global pacing reservation can unnecessarily wait one full interval
**Location:** `src/MailLoadTester.Core/SmartPaceController.cs` / global pacing path

The global pacing implementation reserves future slots. The first reservation must be verified to start immediately when no prior reservation exists; otherwise the first send is delayed by one complete interval. This is a latency/throughput correctness issue for configured interval pacing.

**Status:** OPEN pending targeted runtime/test verification.

### BUG-004 — MEDIUM — Proxy rotation can sample blocked endpoints repeatedly
**Location:** `src/MailLoadTester.Core/ProxyRotator.cs`

Random proxy selection can select from the full collection with replacement while blocked entries remain eligible for the random draw. Under partial bans this can repeatedly sample blocked proxies and can report no usable proxy even though an unblocked endpoint exists.

**Impact:** avoidable connection failures and false exhaustion of the proxy pool.

**Status:** OPEN

### BUG-005 — MEDIUM — IPv4 `/31` handling can lose one usable address
**Location:** `src/MailLoadTester.Core/IpV4Rotator.cs`

CIDR expansion needs explicit handling for `/31`. Under RFC 3021 semantics both addresses in a point-to-point `/31` are usable. Treating the network/broadcast pair using ordinary subnet rules can return only one address. `/32` remains a single address.

**Status:** OPEN

### BUG-006 — MEDIUM — Repository integrity/build layout must stay protected
**Location:** repository structure

The historical `load` repository contained flattened source files and Git-internal artifacts. `load2` has now been verified to contain the intended project structure and a solution referencing the structured project paths. This finding remains as a repository-integrity control item: future changes must not reintroduce flattened source files or Git-internal files.

**Status:** OPEN as preventive/integrity item; `load2` structure itself is currently CORRECT.

### BUG-007 — HIGH — Retry delay bypasses global pacing
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

The initial send reserves the pacing slot through `SmartPaceController.WaitBeforeSendAsync`, but a transient failure enters the retry loop and waits only through exponential `Task.Delay(GetRetryDelay(...))`. The retry does not acquire a new global pacing slot before the next SMTP send.

**Impact:** retries can create bursts and violate the configured global send spacing even though normal sends respect the pacing controller.

**Status:** OPEN

### BUG-008 — HIGH — Direct MX delivery resolves only the first recipient domain
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

In Direct MX mode the code resolves MX only for `options.Recipients[0]` and then replaces `options.SmtpHost` with that MX host. The normal message loop subsequently selects recipients independently from the recipient list. Therefore a mixed-domain recipient list can send messages for other domains to the MX selected for the first domain.

**Impact:** incorrect SMTP routing for multi-domain test sets; results do not represent the actual destination MX path and messages may be rejected or misrouted.

**Status:** OPEN

## Audit round — concurrency / circuit breaker

### SmtpConnectionPool
The current `load2` implementation was inspected for permit ownership, lease/return/discard transitions, shutdown races, and in-flight connection handling. No additional confirmed defect was added in this pass. The implementation explicitly protects lease state and the shutdown hand-off with lifecycle synchronization.

### AdaptiveConcurrencyLimiter
Inspected acquire/cancel/release/reset behavior. The waiter cancellation path and permit hand-off are synchronized under the same lock, and `Reset()` intentionally preserves `_active`. No additional confirmed defect was established in this pass.

### CircuitBreaker
Inspected sliding-window state, cooldown reset, per-category openings, and conditional removal of cooldown entries. The implementation uses conditional removal to avoid erasing a newer opening. No additional confirmed defect was established in this pass.

## Verification blockers

The repository structure is now confirmed, but source inspection alone cannot establish a clean build/test result. The next verification step on Windows is:

```text
dotnet test
dotnet build -c Release
```

Recommended smoke coverage:

- Start → Stop during SMTP connect
- Direct MX with one domain
- Direct MX with multiple recipient domains
- proxy list with partially blocked endpoints
- `/31` and `/32` IPv4 rotation
- AutoRestart after partial failure
- retry pacing with `IntervalMs > 0`
- dry-run with high `MessageCount` and `MaxConcurrency`
- profile round-trip

## Audit rule

Only confirmed defects are added here. Suspicions remain outside the confirmed list until source/caller/test evidence establishes the failure mode.
