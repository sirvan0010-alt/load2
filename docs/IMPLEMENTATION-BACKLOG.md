# load2 — SMTP-first Implementation Backlog

**Status:** planning derived from the current source/audit matrix.  
**Scope:** SMTP/email engine only.  
**Authority:** source code + tests on `main` remain authoritative; this document does not claim implementation is complete.

## Priority 0 — preserve existing safety and architecture

- Keep explicit target/scope and authorization boundaries intact.
- Keep `--unauthorized`, DryRun/TestMode, PathSecurity and AUTH-log redaction behavior intact.
- Keep bounded workers, actual-SEND pacing, adaptive concurrency, cancellation and ledger behavior intact.
- No hardcoded credentials.
- No opaque MSBuild/Roslyn build tasks or native in-memory loaders.
- No force-push CI automation on `main`.

## Priority 1 — FEAT-HEALTH

**Goal:** expose a unified health model for SMTP accounts/sessions and transport outcomes.

### Design

```text
SmtpAccount/Session
    -> connection/auth/send outcome
    -> typed failure classification
    -> health score/state
    -> quarantine / recovery
    -> provider/account selection
```

### Requirements

- Reuse existing SMTP pool/health infrastructure where present.
- Distinguish DNS/connect/TLS/auth/protocol/throttle/recipient/send failures.
- Do not treat every exception as the same failure.
- Health updates must be thread-safe.
- Quarantine must have deterministic recovery rules.
- Respect `CancellationToken`.

### Tests

- failure classification
- concurrent health updates
- quarantine/recovery
- cancellation
- account selection after unhealthy session

## Priority 2 — FEAT-REPORT

**Goal:** make each run independently auditable and machine-readable.

### Design

```text
Scenario
 -> RunId
 -> per-target/per-account outcomes
 -> timing phases
 -> counters
 -> health/retry/pacing events
 -> final RunResult
 -> JSON + console/GUI projections
```

### Requirements

- stable `RunId`
- no credentials/secrets in output
- preserve DeliveryLedger integration
- aggregate results without race conditions
- export deterministic JSON suitable for later analysis
- retain FEAT-022 phase timings already present in the project

### Tests

- deterministic serialization
- concurrent result recording
- secret redaction
- empty/partial runs
- cancellation/failure finalization

## Priority 3 — multi-account SMTP session pool

**Goal:** generalize the existing multi-account capability instead of copying the external tools' duplicated worker pattern.

### Design

```text
AccountRegistry
  -> AccountPolicy
  -> SmtpSessionPool
       -> persistent MailKit session(s)
       -> health/quarantine
       -> bounded worker ownership
```

### Requirements

- configuration from environment/configuration provider
- no plaintext password UI storage
- persistent MailKit sessions where appropriate
- bounded concurrency
- account/provider health
- TLS policy explicit and secure
- graceful reconnect/dispose
- `CancellationToken`

### Tests

- concurrent acquisition/release
- reconnect after transport failure
- account quarantine
- cancellation during acquisition/send
- disposal/lifecycle

## Priority 4 — connection-churn scenario

**Goal:** retain the useful benchmark mechanism found in external SMTP bombers without importing their abuse semantics.

- Add a bounded, explicitly named SMTP connection-churn scenario only if the current engine lacks equivalent coverage.
- Use a hard message/connection limit and/or deadline.
- Continue to use explicit target scope and authorization.
- Measure connection setup, TLS and send timings separately.
- Never make infinite repetition the default.

## Priority 5 — provider/transport registry refinement

**Goal:** make SMTP endpoint configuration a first-class transport concern.

- Provider identity/capabilities.
- Host/port/TLS policy from configuration.
- Capability-aware session setup.
- Typed transport failures.
- Health/quarantine integration.
- No automatic discovery of unrelated public infrastructure.

## Priority 6 — diagnostics/enrichment

Candidate mechanisms from audited projects:

- DNS/SPF/DKIM/DMARC evidence collection.
- TLS certificate/protocol diagnostics.
- SMTP capability/EHLO inspection.
- Open-relay check as an explicitly authorized diagnostic, not an abuse workflow.
- Structured evidence attached to `RunResult`.

These should be implemented as diagnostics/verification components and must not silently turn into target discovery or mass-send behavior.

## Deferred / not in this SMTP-first backlog

- SMS sender implementation.
- WhatsApp sender implementation.
- Call/voice sender implementation.
- Public OTP endpoint collections.
- CAPTCHA/OTP bypass.
- Credential harvesting.
- Stealth/evasion mechanisms.
- Unrestricted public-target flooding.
- Botnet/DDoS orchestration.

The external audits remain valuable for their mechanisms (provider registry, bounded workers, pacing, retry/backoff, health/quarantine, session lifecycle, structured outcomes), but those mechanisms are implemented only inside load2's existing authorization, scope, cancellation and observability model.

## Implementation order

1. **FEAT-HEALTH** — unify/verify current health behavior.
2. **FEAT-REPORT** — RunId + structured report/ledger projection.
3. **Multi-account SMTP session pool** — only where current code still has gaps.
4. **Connection-churn scenario** — only after the existing engine contract is verified.
5. **Provider/transport registry refinement.**
6. **DNS/TLS/SMTP security diagnostics.**

## Definition of Done for each item

- Inspect current source first; do not duplicate existing functionality.
- Implement one coherent mechanism per change.
- Add focused unit/integration tests.
- Verify cancellation and thread safety.
- Verify secret redaction.
- Commit to `main` only after tests/CI are checked.
- Do not claim PASS/FIXED without actual CI evidence.
