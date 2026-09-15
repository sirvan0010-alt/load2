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

1. **B1 Documentation reset and canonical map — COMPLETE.** Competing active plans were consolidated into the canonical documentation set; historical audit material remains evidence/history.
2. **B2 External repository mechanism audit — COMPLETE for EXT-AUDIT-001.** The retained repository set and the explicitly queued slowhttptest/GoldenEye research have source-backed transfer decisions. Future repositories enter a new audit item rather than reopening B2.
3. **B3 Scenario Engine.** Introduce a typed scenario model without creating a second queue, pacing or concurrency system.
4. **B4 Provider Simulator.** Model multiple mail providers and message workflows locally/inside an authorized lab.
5. **B5 Behavioral Analysis.** Measure burst velocity, sender/domain diversity, recipient concentration, provider diversity, authentication distribution and mailbox pressure.
6. **B6 Mailbox/Deliverability Lab.** Integrate a controlled SMTP/IMAP test environment when useful.
7. **B7 Authentication and transport evidence.** Expand SPF/DKIM/DMARC, TLS and related diagnostics from read-only checks into evidence-rich test results.
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

`rojberr/mailcannon` is retained as a **reference only**. Its distributed Docker/Swarm/Kubernetes scaling model is not a P1 dependency for a one-PC load2 deployment. Only an isolated mechanism with a demonstrated benefit may be adapted later.

## Scenario families

The first scenario catalog is:

- `NormalDelivery`
- `BurstDelivery`
- `SustainedLoad`
- `ConnectionSaturation`
- `ProviderDistribution`
- `FailureInjection`
- `MailboxQuota`
- `Deliverability`
- `SubscriptionBombSimulation`
- `DoubleOptInSimulation`
- `AntiAbuseControlSimulation`

`SubscriptionBombSimulation` and related scenarios are simulations against controlled recipients/providers. They are not third-party registration automation.

## Architecture rule

```text
ScenarioDefinition
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
RunObservability + security analysis
```

No parallel queue, pacing stack or retry pipeline is permitted.

## Definition of done for each B item

A feature is not complete until source, focused tests, security review, CI evidence and documentation are synchronized. A README claim or design sketch alone never changes the capability status.

## Next execution order

```text
B3 Scenario Engine
→ B4 Provider Simulator integration
→ B5 Behavioral Analyzer
→ B6 Mailbox/Deliverability Lab
→ B7 authentication/transport evidence
→ B8 replayable artifacts
→ B9 execution gates
```

B3/B4 foundations may exist before their integration is marked complete; status follows source + tests + CI, not document presence.
