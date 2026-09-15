# load2 — Implementation Backlog

**Authority:** source + tests on `main`.  
**Canonical roadmap:** `docs/LOAD2-ROADMAP.md`.  
**Current phase:** TRACK B post-baseline.

## TRACK A — CLOSED

A1–A8 are complete and remain the protected baseline.

## P0 — baseline integrity

- preserve green CI for the verified agent-runtime Docker boundary;
- keep regression coverage for A1–A8;
- keep `--unauthorized`, DryRun/TestMode, hard limits, cancellation and secret redaction mandatory.

## P1 — TRACK B

### B1 Documentation reset — COMPLETE

`docs/DOCUMENTATION-MAP.md` is the canonical map. Historical audit/version files are evidence/history rather than competing plans.

### B2 External repository audit — COMPLETE

EXT-AUDIT-001 is closed. Future repositories are separate audit items. `rojberr/mailcannon` is reference-only for the current one-PC deployment.

### B3 Scenario Engine — COMPLETE

Implemented in `src/MailLoadTester.Core/ScenarioEngine.cs`.

Delivered:

- typed `LoadScenarioKind` / immutable `LoadScenarioDefinition`;
- bounded `ScenarioTuning` with validation;
- central `LoadScenarioCatalog`;
- `ScenarioEngine` adapter over the existing `SmtpTestRunner`;
- direct support for `NormalDelivery`, `BurstDelivery`, `SustainedLoad`, `ConnectionSaturation`, `ProviderDistribution` and `Deliverability`;
- explicit simulation-only boundary for future lab scenarios;
- no second queue, pacing/limiting stack, concurrency stack or retry policy;
- existing cancellation, hard limits and `MailTestResult` remain authoritative;
- focused `ScenarioEngineTests`.

Simulation-only definitions remain unavailable through direct SMTP execution until their controlled provider/mailbox implementation exists:

- `FailureInjection`
- `MailboxQuota`
- `SubscriptionBombSimulation`
- `DoubleOptInSimulation`
- `AntiAbuseControlSimulation`
- `DistributedLab`

### B4 Provider Simulator — NEXT

Integrate deterministic provider behavior with B3 scenarios. Provider simulation must remain local/controlled and must not bypass existing execution controls.

Acceptance:

- deterministic seed;
- provider identity/workflow model;
- throttling/transient failure/latency/authentication-result events;
- no arbitrary external-provider calls;
- composition with B3;
- focused tests;
- CI green.

### B5 Behavioral Analyzer — POST-BASELINE

Normalize transport/provider/mailbox events and calculate evidence for velocity, burst duration, sender/domain diversity, recipient concentration, provider diversity, authentication results, throttling/rejection and mailbox pressure.

Output: `Normal | Elevated | Suspicious | HighRisk`.

### B6 Mailbox / Deliverability Lab — POST-BASELINE

Use a controlled SMTP/IMAP environment for delivery, mailbox quota, recovery and authentication testing. Keep the MTA/test environment outside the core transport layer.

### B7 Authentication / Transport Evidence — POST-BASELINE

Extend existing MX/SPF/DKIM/DMARC/TLS diagnostics into evidence-rich scenario results without creating a second diagnostics engine.

### B8 Replayable Artifacts — POST-BASELINE

Persist redacted scenario/configuration/event/result artifacts sufficient for deterministic reproduction without secrets or unnecessary personal data.

### B9 Security Execution Gates — POST-BASELINE

Enforce:

```text
SCOPE
→ AUTHORIZATION
→ HARD LIMIT
→ CANCELLATION
→ PACING
→ CONCURRENCY
→ SECRETS
→ EVIDENCE
→ TEST
→ CI
```

## P2 — advanced authorized lab automation

- distributed authorized lab workers;
- controlled proxy/IP diversity as a lab topology dimension;
- registration/Double-Opt-In simulators for owned applications;
- anti-bot/anti-abuse control testing without bypass;
- mailbox saturation/recovery;
- failure injection/replay;
- GUI scenario selection and live observability.

## Explicit non-goals

Do not implement arbitrary third-party registration automation, CAPTCHA/OTP bypass, anti-abuse evasion, real botnet operation, provider-limit evasion, credential/token theft, reflection/amplification or unrestricted public-target flooding/destructive DoS/DDoS.

Use controlled simulations, owned applications, synthetic providers/recipients and bounded lab workers for defensive testing.

## Definition of done

An item becomes `COMPLETE` only when source implementation, focused tests, security review, CI evidence and canonical documentation are synchronized. Historical notes and external README claims cannot close an item.
