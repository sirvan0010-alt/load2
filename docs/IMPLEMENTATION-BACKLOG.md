# load2 — Implementation Backlog

**Authority:** source + tests on `main`.  
**Canonical planning document:** `docs/LOAD2-ROADMAP.md`.

## TRACK A — CLOSED

A1 → A8 are complete and remain the protected baseline.

| Item | Status |
|---|---|
| A1 destination-provider throttling | FIXED |
| A2 multi-account throttling regression | FIXED |
| A3 bounded scenario queue + metrics | FIXED |
| A4 retry / budget + RetryMetrics | FIXED |
| A5 SmtpOutcomeClassifier + OutcomeCounts | FIXED |
| A6 global concurrency audit | PASS |
| A7 RunObservability / RunReport | FIXED |
| A8 final baseline | COMPLETE |

## P0 — baseline integrity

- finish and verify the current agent-runtime CI defect;
- never claim runtime Phase 1.3/2.8 verified without real container CI evidence;
- keep regression coverage for the A1–A8 invariants.

## P1 — TRACK B foundation

### B1 Documentation reset — IMPLEMENTED (documentation foundation)

- `docs/DOCUMENTATION-MAP.md` is the canonical map;
- roadmap, gap matrix and backlog now describe the same TRACK B direction;
- historical audit/version files are explicitly evidence/history;
- status remains evidence-driven.

### B2 External repository audit — IN PROGRESS

For every retained repository:

```text
pinned revision → entry points → execution trace → mechanisms → load2 mapping → decision → evidence
```

Decision tags: `HAVE | GAP | ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`.

`rojberr/mailcannon` is reference-only for the current one-PC deployment; no dependency is planned.

### B3 Scenario Engine — FOUNDATION IMPLEMENTED

Added typed `LoadScenarioKind` / `LoadScenarioDefinition` with validation and authorization semantics. Full integration into the existing scenario runner remains the next implementation step.

Required kinds:

- `NormalDelivery`
- `BurstDelivery`
- `SustainedLoad`
- `ConnectionSaturation`
- `ProviderDistribution`
- `FailureInjection`
- `MailboxQuota`
- `Deliverability`
- controlled security simulations

Acceptance criteria for full completion:

- no second queue;
- no second pacing/limiting system;
- no second retry policy;
- existing `CancellationToken` and hard limits remain effective;
- scenario execution produces existing `MailTestResult`/report evidence;
- focused unit/integration tests pass.

### B4 Provider Simulator — FOUNDATION IMPLEMENTED

Added `IMailProviderSimulator` and deterministic local `DeterministicMailProviderSimulator`. It models provider identity, workflow, throttling/transient failures, latency and authentication-result patterns without network access.

Acceptance criteria for full completion:

- deterministic sequence with a supplied seed;
- no arbitrary external-provider calls;
- composition with B3 scenario execution;
- no credentials embedded;
- focused behavioral tests.

## P1/P2 — security analysis

### B5 Behavioral Analyzer — POST-BASELINE

Analyze normalized events for velocity, burst size/duration, sender/domain diversity, recipient concentration, provider diversity, authentication results, throttling/rejection and mailbox pressure.

Return evidence classification: `Normal | Elevated | Suspicious | HighRisk`.

### B6 Mailbox/Deliverability Lab — POST-BASELINE

Provide a controlled SMTP/IMAP test environment and measure delivery, quota, recovery and authentication behavior. Keep the MTA/test environment outside the core transport layer.

### B7 Authentication/transport evidence — POST-BASELINE

Expand evidence for MX, SPF, DKIM, DMARC and TLS-related checks without duplicating existing diagnostic primitives.

### B8 Replayable artifacts — POST-BASELINE

Persist redacted scenario/configuration/event/result artifacts sufficient to reproduce a run without storing credentials or unnecessary personal data.

### B9 Security gates — POST-BASELINE

Enforce:

```text
SCOPE → AUTHORIZATION → HARD LIMIT → CANCELLATION → PACING → CONCURRENCY → SECRETS → EVIDENCE → TEST → CI
```

## Controlled-security requirements

The framework may test abuse patterns defensively in an owned or explicitly authorized environment.

Do not implement third-party registration automation, CAPTCHA/OTP bypass, anti-abuse evasion, real botnets, provider-limit evasion or unrestricted public-target flooding/DoS/DDoS launchers.

Use synthetic providers, controlled mailboxes, owned test applications and bounded lab workers instead.

## Definition of done

No item becomes `COMPLETE` until source implementation, focused tests, security review, CI evidence and canonical documentation are synchronized. Historical notes and external README claims cannot close backlog items.
