# MailLoadTester — MASTER FIX / EVOLUTION PLAN

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> This is the active engineering plan. Historical audit files remain evidence/history only. Before changing code, verify the current `main` implementation and tests.

## Repository authority

- `sirvan0010-alt/load2` / `main` = sole source of truth.
- `sirvan0010-alt/load` = secondary/parallel copy only.
- `sirvan0010-alt/Load-tester-` = legacy; do not use for new implementation.

## Engineering rules

- No invented behavior: verify source, callers and tests.
- Preserve asynchronous/thread-safe C# with `SemaphoreSlim`, `Volatile` where appropriate and `CancellationToken`.
- Keep network/SMTP transport separate from MIME/payload generation.
- Preserve `IMailPayloadPlugin` architecture.
- Credentials/secrets come from configuration/environment; never hardcode them.
- `--unauthorized` remains the explicit live-send authorization acknowledgement outside TestMode/DryRun.
- A concrete recipient supplied by the user is a valid scenario input; do not silently replace the application's target model with an invented allowlist policy. Authorization must nevertheless remain explicit and auditable.
- Do not claim build/test status without actual execution or trustworthy CI evidence.

## Current core execution invariants

The repaired runner uses:

`bounded Channel → fixed workers → PerRecipientLimiter → adaptive concurrency → SMTP pool → actual-SEND gate/pacing → SendAsync`

The actual-SEND gate is exclusive. The first send is immediate. Retries return through the same admission and pacing path. Do not move pacing back ahead of adaptive/pool admission or recreate one task per message.

## Current repair status

### BUG-001 — worker bound / task pressure
**Status: FIXED / merged.**

Bounded channel + fixed worker pool replaces task-per-message scheduling.

### BUG-002 — AutoRestart duplicate delivery
**Status: FIXED / implemented.**

`DeliveryLedger` tracks accepted logical messages across restart cycles.

### BUG-003 — first global pacing reservation
**Status: FIXED / regression-tested.**

The first actual send is immediate; concurrent reservations preserve spacing.

### BUG-004 — blocked proxy random selection
**Status: FIXED / regression-tested.**

Blocked endpoints are excluded from random and round-robin selection.

### BUG-005 — IPv4 `/31`
**Status: FIXED / regression-tested.**

`/31` retains both RFC 3021 point-to-point addresses; `/32` retains its single address.

### BUG-006 — repository integrity
**Status: IMPLEMENTED / test-backed.**

Repository-integrity controls remain part of the release gate.

### BUG-007 — retry pacing bypass
**Status: FIXED / regression-tested.**

Every retry must reacquire the same actual-SEND pacing path.

### BUG-008 — Direct-MX recipient-domain handling
**Status: HARDENED.**

Current public validation/routing constraints must be preserved and tested. Do not infer broader multi-domain support than the current implementation actually provides.

### BUG-009 — pacing before downstream admission
**Status: FIXED / regression-tested.**

Actual-SEND pacing occurs after adaptive concurrency and SMTP-pool admission.

## Next engineering round

### 1. Source-level external capability transfer

Audit the requested external repositories at source level. For each concrete class/function/module record:

`repository → file → symbol → mechanism → measured/claimed behavior → comparison with load2 → transfer decision → target location → tests`

Do not stop at README summaries. Offensive/load-generator repositories are valid technical references for queueing, provider abstraction, concurrency, retries, endpoint health, fan-out, scenario orchestration, failure classification, reporting and reproducibility.

### 2. Controlled capability implementation

Where an external mechanism is useful, adapt it to the existing `load2` model instead of copying its language/runtime assumptions. Candidate capabilities include:

- transport/provider registry;
- endpoint normalization/deduplication;
- provider and endpoint health/quarantine;
- configurable scenario definitions;
- verify/execute separation;
- high-throughput bounded dispatch;
- per-target metrics;
- failure classification;
- retry policy;
- deterministic replay and run artifacts;
- SMTP/TLS/DNS diagnostics;
- controlled repeated-send and stress scenarios.

### 3. Security and correctness verification

Every new capability must preserve authorization, cancellation, bounded worker ownership, pacing, retry accounting, secret redaction and resource cleanup. Do not weaken an existing invariant merely to match an external tool.

### 4. Network/TLS matrix

Continue integration coverage for SMTP `None`, STARTTLS and implicit TLS, authentication failures, certificate failures, proxy/source-IP combinations and Direct-MX behavior where a controlled test service is available.

### 5. Release gate

Require Release build/test, targeted regression tests, concurrency/cancellation stress, network/TLS tests, security tests, repository-integrity review and green CI/CodeQL before declaring the round complete.

## External capability boundary

The audit may fully study offensive mechanics. The implementation decision is made per mechanism, not per repository label.

Useful engineering mechanisms may be adopted/adapted. Mechanisms whose primary purpose is credential theft, stealth, CAPTCHA/OTP bypass, abuse-control evasion, arbitrary public-target discovery for flooding, or uncontrolled destructive traffic are not transferred as such.
