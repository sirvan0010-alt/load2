# load2 — Security Scenario Catalog

**Authority:** source + tests on `main`  
**Phase:** TRACK B — B3 COMPLETE, B4 NEXT

| Scenario | B3 status | Purpose | Execution target | Priority |
|---|---|---|---|---|
| `NormalDelivery` | DIRECT | baseline delivery behavior | owned/authorized SMTP | P1 |
| `BurstDelivery` | DIRECT | short high-rate burst response | owned/authorized SMTP | P1 |
| `SustainedLoad` | DIRECT | long-duration throughput stability | owned/authorized SMTP | P1 |
| `ConnectionSaturation` | DIRECT | bounded connection/session pressure | owned/authorized SMTP | P1 |
| `ProviderDistribution` | DIRECT | provider diversity and throttling behavior | configured SMTP accounts/endpoints | P1 |
| `FailureInjection` | SIMULATION | transient/permanent failure recovery | controlled fixture | P1 |
| `MailboxQuota` | SIMULATION | mailbox capacity and recovery | controlled mailbox | P1 |
| `Deliverability` | DIRECT | transport/authentication evidence | owned/authorized domains | P1 |
| `SubscriptionBombSimulation` | SIMULATION | detect concentrated unsolicited-subscription pattern | simulated providers + controlled mailbox | P2 |
| `DoubleOptInSimulation` | SIMULATION | test confirmation workflow resilience | owned test application | P2 |
| `AntiAbuseControlSimulation` | SIMULATION | test rate/CAPTCHA/OTP control behavior | owned test application | P2 |
| `DistributedLab` | SIMULATION | multi-worker reproducibility | owned lab workers | P2 |

## B3 execution contract

Direct scenarios are represented by `LoadScenarioDefinition` and executed through `ScenarioEngine`. `ScenarioEngine` delegates to the existing `SmtpTestRunner`; it does not introduce another queue, pacing system, concurrency limiter or retry policy.

The direct path therefore remains:

```text
LoadScenarioDefinition
    ↓
ScenarioEngine
    ↓
existing bounded Channel / workers
    ↓
SmartPaceController + existing gates
    ↓
SMTP account/session pools
    ↓
existing SEND / outcome classification
    ↓
MailTestResult / RunReport / RunObservability
```

`ProviderDistribution` requires at least two configured SMTP accounts/endpoints. `BurstDelivery`, `SustainedLoad` and `ConnectionSaturation` only tune controls that already exist in `MailTestOptions`.

## Simulation boundary

Simulation-only scenarios are typed now so the product has one stable scenario vocabulary, but the direct SMTP engine deliberately rejects them until their controlled provider/mailbox implementation exists.

This prevents a future security scenario from silently becoming real external traffic.

## Rules

1. Every scenario is bounded by the existing load2 execution controls.
2. No scenario may create an unrestricted public-target flood.
3. Simulation scenarios default to synthetic recipients/providers.
4. External infrastructure requires explicit authorization and target scope.
5. CAPTCHA and OTP are **controls under test**, never bypass targets.
6. Proxy diversity is a lab topology dimension, not an anti-abuse evasion feature.
7. Each scenario must eventually produce deterministic, redacted evidence suitable for replay.

## SubscriptionBombSimulation

The simulation models the observable pattern without automating real registrations on third-party services. A provider simulator emits confirmation/welcome/unsubscribe/bounce events toward a controlled recipient mailbox. The behavioral analyzer evaluates velocity, sender/domain diversity, provider diversity, recipient concentration and authentication evidence.

Expected output:

```text
Normal | Elevated | Suspicious | HighRisk
```

The classification is evidence for defensive testing and does not itself authorize additional traffic.
