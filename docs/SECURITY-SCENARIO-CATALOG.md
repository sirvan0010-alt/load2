# load2 — Security Scenario Catalog

**Authority:** source + tests on `main`  
**Phase:** TRACK B design / implementation backlog

| Scenario | Purpose | Execution target | Priority |
|---|---|---|---|
| `NormalDelivery` | baseline delivery behavior | owned SMTP/mail lab | P1 |
| `BurstDelivery` | short high-rate burst response | owned SMTP/mail lab | P1 |
| `SustainedLoad` | long-duration throughput stability | owned SMTP/mail lab | P1 |
| `ConnectionSaturation` | bounded connection/session pressure | owned SMTP/mail lab | P1 |
| `ProviderDistribution` | provider diversity and throttling behavior | simulated/owned providers | P1 |
| `FailureInjection` | transient/permanent failure recovery | controlled fixture | P1 |
| `MailboxQuota` | mailbox capacity and recovery | controlled mailbox | P1 |
| `Deliverability` | transport/authentication evidence | owned/authorized domains | P1 |
| `SubscriptionBombSimulation` | detect concentrated unsolicited-subscription pattern | simulated providers + controlled mailbox | P2 |
| `DoubleOptInSimulation` | test confirmation workflow resilience | owned test application | P2 |
| `AntiAbuseControlSimulation` | test rate/CAPTCHA/OTP control behavior | owned test application | P2 |
| `DistributedLab` | multi-worker reproducibility | owned lab workers | P2 |

## Rules

1. Every scenario is bounded by the existing load2 execution controls.
2. No scenario may create an unrestricted public-target flood.
3. Simulation scenarios must default to synthetic recipients/providers.
4. A scenario may be executed against external infrastructure only when the operator supplies the required authorization and target scope.
5. CAPTCHA and OTP are **control-under-test**, not bypass targets.
6. Proxy diversity is a lab topology dimension, not an anti-abuse evasion feature.
7. Each scenario must produce deterministic, redacted evidence suitable for replay.

## SubscriptionBombSimulation definition

The simulation models the observable pattern without automating real registrations on third-party services. A provider simulator emits confirmation/welcome/unsubscribe/bounce events toward a controlled recipient mailbox. The behavioral analyzer evaluates velocity, sender/domain diversity, provider diversity, recipient concentration and authentication evidence.

Expected output:

```text
Normal | Elevated | Suspicious | HighRisk
```

The classification is evidence for defensive testing and does not itself authorize additional traffic.
