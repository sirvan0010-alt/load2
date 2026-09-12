# Cross-Platform Mechanism Matrix

**Status:** SOURCE-AUDITED synthesis  
**Authority:** `sirvan0010-alt/load2` / `main` is the implementation source of truth.  
**Purpose:** consolidate only mechanisms verified in the external source audits. This document does not authorize direct import of abusive/public-target senders.

## Decision legend

- **ADOPT** — mechanism belongs in load2 architecture.
- **ADAPT** — useful mechanism, redesigned for load2's existing abstractions and controls.
- **HARDEN** — load2 should explicitly enforce or improve the property.
- **EXTRACT** — reusable architectural concept, independent of the original transport.
- **SIMULATE** — useful only as a bounded laboratory scenario with cancellation/deadline/scope controls.
- **REFERENCE** — source is informative, not implementation material.
- **REJECT** — do not transfer the mechanism/implementation.

## Matrix

| Mechanism | SMTP | SMS | Call | WhatsApp | Decision / load2 treatment |
|---|---|---|---|---|---|
| Transport abstraction | proven | proven | proven | proven | **ADOPT** — common transport/provider boundary |
| Provider registry | proven | proven | proven | conceptually applicable | **ADOPT/ADAPT** |
| Provider capability metadata | proven | proven | proven | ADAPT | **ADAPT** |
| Persistent session | proven | provider-dependent | provider-dependent | strongly applicable | **ADOPT** where transport supports it |
| Bounded worker pool | proven in references | proven | proven | ADAPT | **ADOPT** — never thread-per-request |
| Target normalization | applicable | proven | proven | chat/group identity | **HARDEN** |
| Target-set/file input | proven | proven | applicable | applicable | **ADAPT** with canonicalization + scope |
| Finite count | proven | proven | proven | proven | **ADAPT** into Scenario limits |
| Duration/deadline | proven in references | applicable | applicable | applicable | **ADAPT** with `CancellationToken` |
| Cancellation | proven | proven/reference | proven/reference | session lifecycle | **ADOPT** |
| Central pacing/throttle | proven | proven | proven | applicable | **ADOPT** — SmartPaceController / policy layer |
| Per-provider limits | applicable | proven | proven | applicable | **ADOPT** as provider policy |
| Per-target limits | applicable | proven | applicable | applicable | **ADOPT** |
| Retry + backoff | applicable | proven | applicable | reconnect equivalent | **ADOPT** with typed failure classes |
| Provider health | applicable | proven | proven | session health | **ADOPT** |
| Provider quarantine | applicable | proven | proven | session quarantine | **ADOPT** |
| Duplicate removal | proven in node/provider refs | applicable | applicable | applicable | **ADOPT** |
| Timeout | proven | proven | proven | applicable | **ADOPT** |
| Response classification | proven | proven | proven | applicable | **ADOPT** |
| Structured result ledger | applicable | proven | proven | applicable | **ADOPT** — ResultLedger / RunResult |
| Queue / requeue | applicable | proven | applicable | applicable | **ADOPT** |
| Payload abstraction/plugin | proven concept | proven concept | applicable | text/media/template | **ADOPT** via `IMailPayloadPlugin` / transport payload boundary |
| Session lifecycle | proven | proven | proven | strongly stateful | **ADOPT** |
| Multi-account SMTP pool | proven | n/a | n/a | n/a | **ADAPT** — account/session pool |
| SMTP TLS configuration | proven | n/a | n/a | n/a | **ADAPT** into secure transport options |
| Connection-per-message | proven | n/a | n/a | n/a | **SIMULATE** only as connection-churn benchmark |
| Browser automation | reference | reference | reference | proven | **REFERENCE**; no direct dependency in SMTP-first core |
| External discovery/inventory | proven in Mailman/DNS references | applicable | applicable | applicable | **ADAPT** only as inventory, never implicit targeting |
| DNS evidence/correlation | proven | n/a | n/a | n/a | **ADAPT** for authorized diagnostics |
| Infinite loop / unlimited repetition | proven | proven | possible | possible | **SIMULATE** only with hard bounds + cancellation |
| Thread-per-request | seen | seen | seen | n/a | **REJECT** — bounded workers instead |
| Public OTP endpoints | seen | seen | seen | n/a | **REJECT** |
| Arbitrary public-target bombing | seen | seen | seen | possible | **REJECT** |
| Credential collection in CLI/GUI | seen | seen | seen | session auth | **REJECT**; environment/secret provider only |
| Obfuscated loaders / opaque build execution | security finding | n/a | n/a | n/a | **REJECT — SECURITY CRITICAL** |
| Force-push automation on `main` | security finding | n/a | n/a | n/a | **REJECT / HARDEN** |

## SMTP-first implementation boundary

The current implementation track remains SMTP-first:

```text
Scenario
  -> TargetSet
  -> Payload / IMailPayloadPlugin
  -> Provider / SMTP account pool
  -> bounded Channel
  -> worker
  -> persistent MailKit SMTP session
  -> PerRecipientLimiter
  -> SmartPaceController
  -> Send
  -> typed outcome
  -> ResultLedger / telemetry
```

SMS, WhatsApp and Call findings are retained as architectural input only. No SMS/WhatsApp/Call sender or public OTP endpoint is introduced by this matrix.

## Supply-chain rules

1. No opaque MSBuild/Roslyn task that decodes or executes native memory during build.
2. No hardcoded credentials or plaintext credential UI storage.
3. No force-push CI automation on `main`.
4. External repositories are audited at mechanism level; README claims alone are not implementation evidence.
5. Offensive mechanisms are evaluated individually. A mechanism may be transferred when it is useful to authorized load testing and is placed behind load2's authorization, scope, pacing, concurrency, cancellation and telemetry controls.

## Source-audit references

The matrix is synthesized from the per-repository audits under `docs/external-repos/`, including SMTP, SMS, Call, WhatsApp and provider/orchestration references. Where a source was only README-level or obfuscated, the matrix records that limitation rather than inventing internals.
