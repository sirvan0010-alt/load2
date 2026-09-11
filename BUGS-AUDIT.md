# BUGS-AUDIT — MailLoadTester

## Audit baseline

- Repository: `sirvan0010-alt/load2`
- Branch: `main`
- `load2` is the sole source of truth.
- `load` is secondary/parallel only.
- `Load-tester-` is legacy and must not be used for new implementation.
- Phase 1–7 source audit is complete; Phase 8 verification is still pending.
- A defect is marked fixed only with source evidence, caller review, regression coverage and verification evidence.

## Confirmed / registered findings

### BUG-001 — HIGH — MaxConcurrency is not a global execution bound
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

`RunSingleAsync` creates one asynchronous task for every message in a batch. The SMTP pool limits leased SMTP clients but does not bound queued tasks, MIME generation, pacing, dry-run work or other per-message processing. `MaxConcurrency` is therefore not a strict global worker bound.

**Impact:** high `MessageCount` can create excessive task/state/CPU/memory pressure.

**Status:** OPEN

### BUG-002 — HIGH — Auto-restart can duplicate successful deliveries
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

`RunAsync` reruns the complete message set after a mostly-failed attempt. There is no per-message delivery ledger, so messages already accepted by the SMTP server can be sent again.

**Impact:** duplicate deliveries and ambiguous unique-message accounting.

**Status:** OPEN

### BUG-003 — MEDIUM — First global pacing reservation waits one full interval
**Location:** `src/MailLoadTester.Core/SmartPaceController.cs`

`ReserveGlobalSlot()` sets the first slot to `now + intervalTicks` because `_schedule.Last?.Value ?? now` is followed unconditionally by `reservedSlot = nextAvailable + intervalTicks`. With an empty schedule and a positive interval, the very first message therefore waits a full configured interval instead of starting immediately.

**Impact:** unnecessary startup latency and lower measured throughput for short tests.

**Status:** CONFIRMED — OPEN

### BUG-004 — MEDIUM — Proxy rotation can sample blocked endpoints repeatedly
**Location:** `src/MailLoadTester.Core/ProxyRotator.cs`

Random selection can draw from the full collection while blocked entries remain eligible. Under partial bans this can repeatedly choose blocked endpoints or falsely exhaust the pool.

**Impact:** avoidable connection failures and false proxy exhaustion.

**Status:** OPEN

### BUG-005 — MEDIUM — IPv4 `/31` handling can lose one usable address
**Location:** `src/MailLoadTester.Core/IpV4Rotator.cs`

`/31` needs explicit RFC 3021 point-to-point semantics: both addresses are usable. Ordinary network/broadcast handling can otherwise discard one address. `/32` remains one address.

**Status:** OPEN

### BUG-006 — MEDIUM — Repository integrity/build layout must stay protected
**Location:** repository structure

`load2` currently has the intended structured solution layout. The finding is retained as a preventive control against reintroducing flattened source files or Git-internal artifacts.

**Status:** OPEN — preventive/integrity control

### BUG-007 — HIGH — Retry delay bypasses global pacing
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

Initial sends reserve a slot through `SmartPaceController.WaitBeforeSendAsync`, while retry attempts wait only through exponential `Task.Delay(...)`. A retry therefore does not reserve a new global pacing slot.

**Impact:** retries can burst and violate configured global spacing.

**Status:** OPEN

### BUG-008 — HIGH — Direct MX implementation resolves only the first recipient domain
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

The runner's Direct MX path resolves the first recipient's domain and replaces `SmtpHost` with that MX. However, current `Validation.Validate()` rejects a Direct MX configuration containing more than one recipient domain before the runner proceeds. Therefore the mixed-domain defect is currently blocked at the public validation boundary, but the runner logic remains structurally unsafe if validation is bypassed or reused differently in future code.

**Impact:** without the validation guard, mixed-domain messages could be routed to the first domain's MX.

**Status:** OPEN — validation mitigation present; runner hardening still required

### BUG-009 — HIGH — Global pacing reserves time before concurrency/pool admission, so actual SEND spacing is not guaranteed
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs` + `src/MailLoadTester.Core/SmartPaceController.cs`

The runner calls `WaitBeforeSendAsync()` first. That method reserves and waits for a global schedule slot, then the worker separately waits for `RateLimiter`/`AdaptiveConcurrencyLimiter` and, for real SMTP, `SmtpConnectionPool.RentAsync()`. A worker can therefore receive an earlier pacing slot, become delayed behind adaptive concurrency or SMTP connection admission, while a later worker reaches `SendAsync()` first. The configured global interval is consequently a reservation spacing, not a guaranteed spacing between actual SMTP sends.

**Impact:** under contention or connection churn, observed SMTP SEND timestamps can violate the configured global spacing even though the pacing controller itself is internally synchronized.

**Required regression:** deterministic test with a controlled second-stage delay proving actual send order/timestamps cannot collapse below the configured spacing.

**Status:** CONFIRMED — OPEN

## Investigation items — not promoted to confirmed bugs

### SEC-AUDIT-001 — AUTH secret redaction — NOT VERIFIED
`SessionProtocolLogger` receives SMTP client protocol bytes and exposes `AuthenticationSecretDetector`, but source inspection alone does not establish end-to-end masking for the custom logger. A fake-SMTP AUTH LOGIN/PLAIN integration test must prove that plaintext and encoded credentials never reach the session log. Do not label this a confirmed leak until the runtime evidence exists.

### SEC-AUDIT-002 — Filesystem path boundary — NOT VERIFIED
Complete audit still requires UNC, junction/symlink and canonical-path behavior across EML templates, attachments, inline attachments, profiles, exports and webhook-related paths.

### SEC-AUDIT-003 — Secret provenance — NOT VERIFIED
Verify all credential/password inputs and configuration paths; source comments are not sufficient evidence that no hardcoded secret exists elsewhere in the application.

### SEC-AUDIT-004 — `--unauthorized` gate — NOT VERIFIED
Verify the CLI/network execution gate end-to-end and prove DryRun performs no network send.

### CONC-AUDIT-001 — Cross-component concurrency — NOT VERIFIED
The individual limiter/pool inspections found no additional confirmed race, but the complete combined state machine requires runtime stress/cancellation evidence.

### CONC-AUDIT-002 — Core cancellation — NOT VERIFIED
Complete token propagation and cleanup verification remains part of Phase 8 regression execution.

### NET-AUDIT-001 — Network interaction matrix — NOT VERIFIED
Source-IP rotation, proxy selection and IPv4/IPv6 socket-family combinations require integration coverage.

### NET-AUDIT-002 — TLS matrix — NOT VERIFIED
Plain/STARTTLS/implicit TLS, certificate validation, client certificates, timeout and cancellation transitions require runtime coverage.

## Phase 1–7 conclusion

The targeted deep audit promoted two additional source-confirmed defects: **BUG-003** (first pacing slot) and **BUG-009** (pacing composition with downstream admission). The confirmed ledger is now BUG-001 through BUG-009.

Phase 8 is the next step: build/test execution plus targeted integration and smoke verification. No item is marked FIXED merely because a code comment claims it is fixed.
