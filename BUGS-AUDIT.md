# BUGS-AUDIT — MailLoadTester

## Audit baseline

- Repository: `sirvan0010-alt/load2`
- Branch: `main`
- `load2` is the sole source of truth.
- `load` is secondary/parallel only.
- `Load-tester-` is legacy and must not be used for new implementation.
- Phase 1–7 source audit is complete; Phase 8 verification is still pending.
- A defect is marked fixed only with source evidence, caller review, regression coverage and verification evidence.
- The repair/audit roadmap is tracked as **9 top-level points**. Subtasks are explicitly numbered `1.1`, `1.2`, etc. so that an expanded point does not get confused with a new top-level phase.

## 9-point repair and audit roadmap

1. **Execution-model refactor and verification** — BUG-001, BUG-003, BUG-007, BUG-009; currently active. Subtasks: 1.1 bounded workers, 1.2 first-send pacing, 1.3 retry pacing, 1.4 actual-SEND gate ordering, 1.5 regression/cancellation review, 1.6 compile/static integrity and branch synchronization.
2. **Delivery correctness / auto-restart** — BUG-002; durable per-message delivery ledger and duplicate-send prevention. Subtasks: 2.1 define message identity and attempt semantics, 2.2 implement in-memory run ledger, 2.3 make restart skip already accepted messages, 2.4 preserve correct aggregate counters, 2.5 add duplicate-prevention regression tests, 2.6 verify cancellation/failure recovery semantics.
3. **Proxy rotation** — BUG-004; blocked endpoint eligibility and exhaustion semantics.
4. **IPv4 rotation** — BUG-005; RFC 3021 `/31` and `/32` semantics.
5. **Repository integrity** — BUG-006; preserve structured solution layout and prevent accidental flattening/Git-internal artifacts.
6. **Direct MX routing hardening** — BUG-008; make runner safe even if validation is bypassed or reused with mixed recipient domains.
7. **Security verification** — SEC-AUDIT-001..004; AUTH redaction, filesystem boundaries, secret provenance, `--unauthorized` and DryRun network isolation.
8. **Concurrency/network/runtime verification** — CONC-AUDIT-001..002 and NET-AUDIT-001..002; combined state-machine stress, cancellation, socket/source-IP/proxy combinations and TLS matrix.
9. **Architecture and release verification** — plugin discovery/lifecycle, options/validation cohesion, production-to-test coverage map, Release build, tests and smoke verification.

## Branch synchronization note

`main` and `fix/bugs-001-003-007-009-execution-model` are intentionally still divergent. The current `main` contains the restored/verified `SmtpTestRunner.cs` candidate source, while the repair branch contains the SmartPace and regression-test work. The two branches must not be force-overwritten. Before active repair code is advanced, the current `main` runner must be reconciled with the repair branch while preserving the repair branch's SmartPace and test changes.

Latest observed comparison: `main` is 9 commits ahead and 13 commits behind the repair branch; the repair branch contains the execution-model patch, SmartPace changes and regression tests, while `main` contains the current runner/documentation changes.

## Confirmed / registered findings

### BUG-001 — HIGH — MaxConcurrency is not a global execution bound
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

`RunSingleAsync` creates one asynchronous task for every message in a batch. The SMTP pool limits leased SMTP clients but does not bound queued tasks, MIME generation, pacing, dry-run work or other per-message processing. `MaxConcurrency` is therefore not a strict global worker bound.

**Impact:** high `MessageCount` can create excessive task/state/CPU/memory pressure.

**Status:** OPEN — verification pending

#### 1.1 — Bounded worker-pool replacement
The current `main` `SmtpTestRunner.cs` now contains the bounded `Channel<int>` worker model with `MaxConcurrency` workers and bounded channel capacity. The source has the intended execution shape, but the fix is not marked fixed until the repaired branch is synchronized and compilation/regression evidence exists.

**Status:** CANDIDATE — STATIC REVIEW POSITIVE, BRANCH SYNC + VERIFICATION PENDING

#### 1.6 — Compile/static integrity and branch synchronization
The previously reported extra-brace/channel-construction candidate errors are no longer present in the current `main` runner. The current `main` source has the complete `try/finally` structure and bounded channel construction. However, the repair branch still contains the older runner blob, so the corrected source must be reconciled into the repair branch without overwriting its SmartPace/test changes.

**Status:** OPEN — BRANCH SYNC / BUILD BLOCKER

### BUG-002 — HIGH — Auto-restart can duplicate successful deliveries
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

`RunAsync` reruns the complete message set after a mostly-failed attempt. There is no per-message delivery ledger, so messages already accepted by the SMTP server can be sent again.

**Impact:** duplicate deliveries and ambiguous unique-message accounting.

**Status:** OPEN

#### 2.1 — Define message identity and attempt semantics
The ledger must identify each requested message deterministically within one logical test run. The identity must remain stable across AutoRestart attempts and must not depend on worker ID, SMTP connection, proxy, or retry count. A per-run ledger is preferred for the first safe implementation; durable cross-process persistence should not be introduced unless the application contract requires resumability after process termination.

**Status:** PLANNED

#### 2.2 — In-memory delivery ledger
Track at least `Pending`, `InFlight`, `Accepted`, and `Failed` (or an equivalent atomic state) per message index. State transitions must be thread-safe and cancellation-safe. `Accepted` means the SMTP server accepted the message through the existing successful `SendAsync` path; it must never be reset to `Pending` by AutoRestart.

**Status:** PLANNED

#### 2.3 — Auto-restart skips accepted messages
On a restart, workers must consume only messages that are not already `Accepted`. The restart must not recreate a fresh full-message work set. This is the primary duplicate-delivery prevention rule.

**Status:** PLANNED

#### 2.4 — Aggregate counters remain semantically correct
`Sent` must count unique SMTP-accepted messages for the logical run, while retry attempts and restart attempts must not inflate unique delivery counts. Attempt-level diagnostics may be retained separately. Existing `MailTestResult` fields must be reviewed before changing their semantics.

**Status:** PLANNED

#### 2.5 — Duplicate-prevention regression coverage
Add deterministic tests proving that a successful message from attempt 1 is not sent again during AutoRestart, while failed/unaccepted messages remain eligible. Also verify that retries for one message do not mark it accepted before the actual SMTP `SendAsync` succeeds.

**Status:** PLANNED

#### 2.6 — Cancellation/failure recovery semantics
Cancellation must not mark `InFlight` as `Accepted`. A message is eligible for another logical attempt only when its prior SMTP send was not accepted according to the existing result/error contract. Ambiguous network outcomes require conservative handling and explicit tests; the implementation must not claim delivery certainty when the SMTP outcome is unknown.

**Status:** PLANNED

### BUG-003 — MEDIUM — First global pacing reservation waits one full interval
**Location:** `src/MailLoadTester.Core/SmartPaceController.cs`

`ReserveGlobalSlot()` set the first slot to `now + intervalTicks`, causing unnecessary startup delay.

**Status:** CONFIRMED — OPEN / verification pending

#### 1.2 — First actual SEND must be immediate
The repair design changes global pacing from pre-reserved schedule slots to an actual-send gate based on the timestamp of the previous real SEND. The first SEND is immediate; subsequent SEND operations are spaced from the previous actual SEND.

**Status:** CANDIDATE — STATIC REVIEW POSITIVE, VERIFICATION PENDING

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

Initial sends reserved a slot through `SmartPaceController.WaitBeforeSendAsync`, while retry attempts waited only through exponential `Task.Delay(...)`. A retry therefore did not reserve a new global pacing slot.

**Status:** OPEN / verification pending

#### 1.3 — Retry must re-enter actual-SEND pacing
The current `main` runner places `AcquireSendSlotAsync()` around each real `SendAsync()` inside the retry loop. Retry backoff remains cancellation-aware and does not itself consume a pacing slot.

**Status:** CANDIDATE — STATIC REVIEW POSITIVE, VERIFICATION PENDING

### BUG-008 — HIGH — Direct MX implementation resolves only the first recipient domain
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs`

The runner's Direct MX path resolves the first recipient's domain and replaces `SmtpHost` with that MX. Current public validation rejects a Direct MX configuration containing more than one recipient domain, but the runner remains structurally unsafe if validation is bypassed or reused.

**Status:** OPEN — validation mitigation present; runner hardening still required

### BUG-009 — HIGH — Global pacing reserves time before concurrency/pool admission, so actual SEND spacing is not guaranteed
**Location:** `src/MailLoadTester.Core/SmtpTestRunner.cs` + `src/MailLoadTester.Core/SmartPaceController.cs`

The old runner called `WaitBeforeSendAsync()` before adaptive concurrency and SMTP-pool admission, so reservation order did not guarantee actual SEND order.

**Status:** CONFIRMED — OPEN / verification pending

#### 1.4 — Actual-SEND gate ordering
The current `main` runner moves the global pacing gate to immediately before the actual SMTP `SendAsync()`, after adaptive-concurrency and SMTP-pool admission. The gate is scoped only around the real SEND and is released immediately afterward.

**Status:** CANDIDATE — STATIC REVIEW POSITIVE, VERIFICATION PENDING

#### 1.5 — Execution-model regression and cancellation review
Regression coverage exists on the repair branch for first-send immediacy, concurrent pacing, gate cancellation, zero interval, recipient limiter interaction, bounded worker concurrency, transient retry pacing and cancellation. Full build/test execution has not yet been performed, so this is coverage evidence rather than execution proof.

**Status:** OPEN — VERIFICATION PENDING

## Investigation items — not promoted to confirmed bugs

### SEC-AUDIT-001 — AUTH secret redaction — NOT VERIFIED
`SessionProtocolLogger` receives SMTP client protocol bytes and exposes `AuthenticationSecretDetector`, but source inspection alone does not establish end-to-end masking for the custom logger. A fake-SMTP AUTH LOGIN/PLAIN integration test must prove that plaintext and encoded credentials never reach the session log.

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

The targeted deep audit promoted two additional source-confirmed defects: **BUG-003** (first pacing slot) and **BUG-009** (pacing composition with downstream admission). The confirmed ledger remains BUG-001 through BUG-009.

## Current execution-model status

Top-level point **1** remains active. The corrected source is present on `main`, but active repair work remains on `fix/bugs-001-003-007-009-execution-model`. Do not force-push one branch over the other. First reconcile the current `main` runner with the repair branch while preserving the repair branch's SmartPace and regression-test changes. Only after that should point 1 receive build/test verification and a FIXED decision.

## Next planned implementation

After safe branch reconciliation, continue with top-level point **2 — BUG-002**. The first implementation target is the per-run, thread-safe delivery ledger described in **2.1–2.6**. The design must preserve the existing SMTP/session architecture, `SemaphoreSlim`/limiter protections, `CancellationToken` flow, plugin separation and `--unauthorized` safety gate. No uncontrolled retry/flooding behavior is to be introduced.

Phase 8 remains the verification stage: Release build, tests, deterministic timing tests, integration/smoke checks and security/runtime evidence. No item is marked FIXED merely because source comments or a candidate branch claim it is fixed.
