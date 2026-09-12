# load2 — External mechanism gap matrix

**Authority:** `sirvan0010-alt/load2` / `main`

## 0. Interpretation rule for external repositories

External repositories are audited by **mechanism**, not by their project label.

A repository named `bomber`, `flooder`, `scanner`, `POC` or similar is **not automatically rejected**. We inspect its actual implementation and ask:

> What concrete mechanism does this code implement, and can that mechanism improve an authorized SMTP/load/security test in `load2`?

The audit therefore covers both defensive and offensive implementations: scheduling, concurrency, provider fan-out, repeated delivery, endpoint rotation, retry behavior, failure handling, protocol robustness, diagnostics, DNS/TLS checks and scenario orchestration.

The implementation boundary is the **authorization and scope model**, not the repository's marketing name. `--unauthorized`, DryRun/TestMode, bounded concurrency, pacing, cancellation and explicit target/scope validation remain mandatory control mechanisms.

### Decision tags

| Tag | Meaning |
|---|---|
| **HAVE** | Capability already exists in `load2`; compare implementations and add tests/hardening only when useful. |
| **GAP** | Useful capability is missing; candidate for implementation. |
| **ADOPT** | External mechanism is suitable for direct architectural/function adoption after source audit. |
| **ADAPT** | Useful mechanism needs to be redesigned for existing `load2` abstractions. |
| **HARDEN** | Existing `load2` capability should be strengthened using evidence from the external implementation. |
| **EXTRACT** | Extract an architectural pattern without importing the original code. |
| **SIMULATE** | Implement a bounded test/failure-injection equivalent rather than an unrestricted action. |
| **REFERENCE** | Useful for comparison/research; no direct implementation currently justified. |
| **REJECT** | Reject the mechanism itself when its primary function is theft, credential/token harvesting, CAPTCHA/OTP bypass, stealth/evasion, arbitrary public-target discovery for abuse, or unrestricted destructive DoS/DDoS/flooding. |

**Important:** `REJECT` applies to a **mechanism**, not automatically to an entire repository. A repository may contain several mechanisms with different decisions.

---

## FEAT-022 — IMPLEMENTED

Phase timing averages on `MailTestResult` (successful messages):

| Field | Meaning |
|---|---|
| `AvgPrepWaitMs` | `WaitBeforeSendAsync` (absolute/recipient gates) |
| `AvgAdaptiveWaitMs` | rate limiter + adaptive acquire |
| `AvgPoolWaitMs` | `SmtpConnectionPool.RentAsync` |
| `AvgPaceWaitMs` | `AcquireSendSlotAsync` |
| `AvgSmtpSendMs` | `SmtpClient.SendAsync` |

Commits: `d0af677` (runner+model) · tests `e8732d6` · `TimingBreakdownTests`

---

## Current capability map

| Mechanism | load2 status | Evidence / next action |
|---|---|---|
| bounded worker queue | HAVE | `Channel<T>` + fixed worker count; preserve invariant |
| adaptive concurrency | HAVE | existing adaptive controller; cross-component tests |
| global actual-SEND pacing | HAVE | `SmartPaceController` + exclusive send gate |
| per-recipient pacing | HAVE | `WaitBeforeSendAsync` / recipient limiter |
| SMTP session pool | HAVE | `SmtpConnectionPool` |
| circuit breaker | HAVE | existing SMTP/network failure protection |
| proxy endpoint quarantine | HAVE | `ProxyRotator` blocked endpoint handling + tests |
| delivery ledger / AutoRestart deduplication | HAVE | logical-message delivery ledger |
| timing breakdown | HAVE | FEAT-022 |
| SMTP endpoint soft health score | GAP | FEAT-HEALTH |
| machine-readable JSON run summary | GAP | FEAT-REPORT |
| RunId + configuration snapshot | GAP | FEAT-RUNID |
| Verify/Execute plugin separation | GAP | FEAT-VERIFY |
| provider/transport registry | GAP | audit current SMTP + Direct-MX abstractions first |
| endpoint canonicalization/deduplication | GAP | extract from provider/node tools |
| scenario model | GAP | explicit authorized target scope |
| failure classification | GAP | formalize beyond exception-only retry decisions |
| replayable run artifacts | GAP | redacted machine-readable artifacts |
| DNS enrichment (MX/SPF/DKIM/DMARC) | GAP | separate diagnostics layer |
| TLS evidence enrichment | GAP | separate transport/security diagnostics |
| failure injection | GAP | SIMULATE in controlled/lab fixtures |

---

## Remaining backlog

| Prio | ID | Work |
|---|---|---|
| 2 | FEAT-HEALTH | Soft SMTP endpoint health score |
| 3 | FEAT-REPORT | JSON run summary |
| 4 | FEAT-RUNID | RunId + config snapshot |
| 5 | FEAT-VERIFY | Plugin `VerifyAsync` |
| — | NET-AUDIT-001 | Live network/TLS matrix when fixtures exist |
| — | EXT-AUDIT-001 | Source-level audit of every retained external repository |

### EXT-AUDIT-001 is mandatory

For every retained external repository, the deep-dive must identify, where the source supports it:

- exact entry point(s);
- relevant file path(s), class/function/method/symbol;
- inputs and configuration;
- network/protocol behavior;
- concurrency and queue model;
- provider/endpoint selection;
- rate limiting and retry behavior;
- timeout and cancellation behavior;
- payload/message generation;
- target handling;
- logging/reporting/persistence;
- dependencies and runtime assumptions;
- useful mechanism(s) and exact `load2` mapping;
- decision tag and reason;
- tests required before adoption.

A README-only review is **not** a source-level audit. If source cannot be inspected, the item remains explicitly marked `AUDIT PENDING` rather than being presented as proven.

---

## Safety / authorization boundary

The project is an authorized testing framework. It may implement controlled equivalents of offensive testing mechanics when they are useful for testing owned, lab or explicitly authorized targets.

Examples of mechanisms that may be investigated/adapted in controlled form:

- repeated-send test scenarios with explicit limits;
- high-concurrency scheduler stress;
- provider failover and endpoint quarantine;
- multi-provider/transport orchestration inside explicit scope;
- bounded retry-storm simulation;
- malformed-message/protocol robustness tests on controlled infrastructure;
- SMTP/TLS failure injection;
- DNS misconfiguration tests on controlled domains;
- open-relay verification on explicitly authorized servers;
- rate-limit detection and reporting;
- cancellation/recovery stress;
- deterministic scenario replay.

The following mechanisms remain **REJECT** regardless of which external repository contains them:

- credential/token theft or harvesting;
- CAPTCHA/OTP bypass;
- stealth/evasion whose purpose is concealing abuse;
- arbitrary public-target discovery for flooding/abuse;
- unrestricted destructive DoS/DDoS launchers;
- mechanisms whose primary purpose is defeating provider abuse controls.

This distinction must be preserved in all future AI instructions and documentation: **do not reject an entire repository because it is offensive; audit and classify its individual mechanisms.**
