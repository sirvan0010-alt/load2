# load2 — Product Roadmap

**Authority:** source + tests on `main`. This document describes the post-TRACK-A direction; it does not replace source truth.

## Product direction

load2 evolves from an SMTP load tester into an **authorized email security and mail-system load-testing framework**.

The existing SMTP engine remains the foundation. New capabilities must reuse its pacing, bounded concurrency, persistent sessions, retry policy, outcome taxonomy, cancellation and evidence pipeline.

## Priority model

### P0 — protect the baseline

1. Keep TRACK A A1–A8 closed and regression-tested.
2. Keep the verified agent-runtime Docker boundary and its CI evidence under regression protection.
3. Keep `--unauthorized`, DryRun/TestMode, hard limits, cancellation and secret redaction mandatory.
4. Keep canonical documentation synchronized with source, tests and CI evidence.

### P1 — controlled security scenarios

1. **B1 Documentation reset and canonical map — COMPLETE.**
2. **B2 External repository mechanism audit — COMPLETE for EXT-AUDIT-001.**
3. **B3 Scenario Engine — COMPLETE.** Typed scenario definitions and a single execution adapter route supported scenarios through the existing SMTP runner.
4. **B4 Provider Simulator — COMPLETE, CI VERIFIED.** Deterministic local provider/workflow simulation with controlled outcomes and B3 composition.
5. **B5 Behavioral Analysis — COMPLETE, CI VERIFIED.** Deterministic burst, velocity, diversity, concentration and anomaly analysis over normalized mail events.
6. **B6 Mailbox/Deliverability Lab — COMPLETE, CI VERIFIED.** Controlled in-memory mailbox lab with quotas, delivery dispositions, pressure observation and recovery. A real SMTP/IMAP MTA remains an external lab adapter, not a second core transport stack.
7. **B7 Authentication and transport evidence — COMPLETE, CI VERIFIED.** Existing transport diagnostics are projected into structured SMTP/TLS/MX/SPF/DMARC evidence with an explicit DKIM selector boundary.
8. **B8 Replayable runs.** Persist redacted scenario, configuration, event and result artifacts for deterministic reproduction.
9. **B9 Security gates.** Enforce SCOPE → AUTHORIZATION → HARD LIMIT → CANCELLATION → PACING → CONCURRENCY → SECRETS → EVIDENCE → TEST → CI.

### P2 — advanced lab automation

- distributed authorized lab workers;
- controlled proxy/IP diversity as a test dimension, not as an anti-abuse evasion mechanism;
- registration/Double-Opt-In workflow simulators;
- anti-bot/anti-abuse control testing against owned applications;
- mailbox saturation and recovery tests;
- failure injection and replay;
- GUI scenario selection and live observability.

## Explicit non-goals

load2 must not automate registrations against arbitrary third-party services, bypass CAPTCHAs or OTPs, defeat anti-abuse controls, operate a real botnet, evade provider limits, or provide an unrestricted public-target flooding/DoS/DDoS launcher.

The corresponding security-testing objectives remain supported through controlled simulators, owned test applications, lab providers and bounded authorized execution.

## Single-machine decision

`rojberr/mailcannon` is retained as a **reference only**. Its distributed Docker/Swarm/Kubernetes scaling model is not a P1 dependency for a one-PC load2 deployment.

## Scenario families

Direct SMTP scenarios supported by B3:

- `NormalDelivery`
- `BurstDelivery`
- `SustainedLoad`
- `ConnectionSaturation`
- `ProviderDistribution`
- `Deliverability`

Provider/lab simulation scenarios:

- `FailureInjection`
- `MailboxQuota`
- `SubscriptionBombSimulation`
- `DoubleOptInSimulation`
- `AntiAbuseControlSimulation`

The simulation-only boundary prevents a future scenario from silently turning into unrestricted real traffic.

## Architecture rule

```text
ScenarioDefinition
    ↓
ScenarioEngine
    ↓
existing bounded scenario Channel
    ↓
existing workers / MaxConcurrency
    ↓
existing SmartPaceController + recipient/provider gates
    ↓
existing SMTP session/account pools
    ↓
existing SEND + outcome classification
    ↓
DeliveryLedger / RetryMetrics / RunReport
    ↓
RunObservability + BehavioralAnalysis
    ↓
controlled MailboxDeliverabilityLab
    ↓
AuthenticationTransportEvidence
```

No parallel queue, pacing stack or retry pipeline is permitted.

## Definition of done for each B item

A feature is not complete until source, focused tests, security review, CI evidence and documentation are synchronized. A README claim or design sketch alone never changes the capability status.

## Next execution order

```text
B8 replayable artifacts
→ B9 execution/security gates
```

B4+ status follows source + tests + CI, not document presence.
