# load2 — AI Guide (single entry point)

**Read this first.** The repository has a large historical audit trail; current work is governed by the canonical documents listed in `docs/DOCUMENTATION-MAP.md`.

**SOURCE OF TRUTH:** `sirvan0010-alt/load2`, branch `main`. Source code + tests take precedence over every document. A working branch is a proposal until merged and verified on `main`.

```text
README → docs/AI-GUIDE.md → docs/DOCUMENTATION-MAP.md → source + tests → verified CI → canonical detail docs → historical evidence
```

## 1. Current baseline

`TRACK A` is **CLOSED**. A1 → A8 are complete. Do not reopen them without concrete regression evidence.

| ID | Status |
|---|---|
| A1 destination-provider throttling | FIXED |
| A2 multi-account throttling regression | FIXED |
| A3 bounded scenario queue + metrics | FIXED |
| A4 retry policy + budget + RetryMetrics | FIXED |
| A5 unified SMTP outcome classification | FIXED |
| A6 global concurrency audit | PASS — no architecture change |
| A7 RunObservability / RunReport projection | FIXED |
| A8 final baseline + documentation | COMPLETE |

## 2. Engine invariants

- One pacing system: `SmartPaceController`.
- One bounded scenario `Channel<T>` and bounded workers governed by `MaxConcurrency`.
- `AcquireSendSlotAsync` gates every real SEND, including retries.
- `DeliveryLedger` keeps `Accepted` terminal across AutoRestart.
- `RetryPolicy` uses the unified `SmtpOutcomeClassifier` and global retry budget.
- One outcome taxonomy feeds retry, health, counters and reporting.
- `MailTestResult` is the source of truth; `RunReport` and `RunObservability` are projections.
- Persistent SMTP sessions remain the normal transport model.
- `CancellationToken`, DryRun/TestMode, `--unauthorized`, hard limits and secret redaction remain mandatory.
- New scenario/provider abstractions must sit above the existing execution engine and must not create a second queue, pacing or retry system.

## 3. TRACK B — product evolution

load2 now evolves toward an **authorized email security and mail-system load-testing framework**. The new work is post-baseline and is deliberately broader than SMTP throughput alone.

Priority order is defined in `docs/LOAD2-ROADMAP.md`.

### B1 — documentation and governance — COMPLETE

- canonical documentation map;
- current roadmap;
- capability/gap matrix;
- implementation backlog;
- mail-security architecture;
- security scenario catalog;
- external-repository transfer audit.

Historical audit files remain evidence, not competing plans.

### B2 — external repository audit — COMPLETE for EXT-AUDIT-001

Evaluate future repositories by concrete mechanism and source evidence, not by repository name. Use:

`HAVE | GAP | ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`

README claims are not implementation evidence. Pin revisions where possible and keep unknowns `PENDING`.

`rojberr/mailcannon` is **REFERENCE only for load2's current one-PC product direction**. Its distributed Docker/Swarm/Kubernetes scaling is not a dependency.

### B3 — scenario engine — COMPLETE

Implemented in `src/MailLoadTester.Core/ScenarioEngine.cs` with focused tests.

Direct scenarios:

- `NormalDelivery`
- `BurstDelivery`
- `SustainedLoad`
- `ConnectionSaturation`
- `ProviderDistribution`
- `Deliverability`

Typed simulation-only scenarios are represented but intentionally rejected by the direct SMTP adapter until their controlled provider/mailbox implementation exists:

- `FailureInjection`
- `MailboxQuota`
- `SubscriptionBombSimulation`
- `DoubleOptInSimulation`
- `AntiAbuseControlSimulation`
- `DistributedLab`

`ScenarioEngine` delegates to the existing `SmtpTestRunner`, preserving the existing bounded queue, workers, pacing, concurrency, SMTP pools, retry policy, cancellation and result/report pipeline.

### B4 — provider simulator — NEXT

Introduce a local/controlled provider simulation layer able to model provider identity, sender population, workflow/message type, acceptance/throttling/rejection, latency/failure and authentication-result patterns.

This is the preferred way to test subscription-bombing and similar behavioral patterns without automating registrations against third-party services.

### B5 — behavioral analysis

Normalize mail events and calculate velocity, burst characteristics, sender/domain diversity, recipient concentration, provider diversity, authentication distribution, throttling/rejection and mailbox pressure. Produce evidence classifications such as `Normal`, `Elevated`, `Suspicious`, `HighRisk`.

### B6/B7/B8/B9

Continue with controlled mailbox/deliverability lab, richer authentication/transport evidence, replayable redacted artifacts and explicit security gates. See the roadmap and architecture documents.

## 4. Controlled-security boundary

The framework may model abuse patterns for defensive testing in an owned or explicitly authorized environment.

Do **not** implement:

- automated registrations against arbitrary third-party services;
- CAPTCHA or OTP bypass;
- anti-abuse evasion;
- real botnet operation;
- provider-limit evasion;
- unrestricted public-target flooding or DoS/DDoS launchers.

Instead implement controlled simulations, owned test applications, synthetic providers/recipients, bounded lab workers and explicitly configured lab proxies where required for a legitimate topology test.

## 5. External research transfer rule

```text
source revision
→ symbol/entry-point map
→ execution trace
→ mechanism inventory
→ load2 comparison
→ decision
→ focused implementation only when justified
→ focused tests
→ CI / CodeQL
→ documentation synchronization
```

## 6. Documentation synchronization

Every verified implementation change updates the relevant canonical documents. Follow `docs/DOCUMENTATION-MAP.md` so historical files do not become parallel specifications.

## 7. Evidence rule

`FIXED`, `PASS`, `COMPLETE` and `VERIFIED` require source/test/CI evidence. Never infer a status from a plan, README, external repository claim or an old audit.
