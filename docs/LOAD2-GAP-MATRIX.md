# load2 — Current Capability and Gap Matrix

**Authority:** `sirvan0010-alt/load2` / `main`  
**Baseline:** TRACK A A1–A8 COMPLETE and protected.  
**Current evolution:** TRACK B post-baseline.

## 0. Status rules

`HAVE` means demonstrated in current source/tests. `FOUNDATION` means the contract exists but full engine integration is not complete. `POST-BASELINE` means planned work. `COMPLETE` requires source/tests/CI/documentation evidence.

External mechanisms use:

`HAVE | GAP | ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`

README claims are never implementation evidence.

## 1. TRACK A — closed

| Area | Status |
|---|---|
| A1 destination-provider throttling | FIXED |
| A2 multi-account throttling regression | FIXED |
| A3 bounded scenario queue + metrics | FIXED |
| A4 retry policy + budget + RetryMetrics | FIXED |
| A5 unified SMTP outcome classification | FIXED |
| A6 global concurrency audit | PASS — no architecture change |
| A7 RunObservability / RunReport | FIXED |
| A8 final baseline/documentation | COMPLETE |
| FEAT-022 timing | IMPLEMENTED |
| Phase 1.3 agent-runtime Docker boundary | VERIFIED by green CI |

## 2. Existing engine capability

| Mechanism | Status | Evidence / treatment |
|---|---|---|
| bounded worker queue | HAVE | bounded `Channel<T>` + workers |
| adaptive concurrency | HAVE | existing `MaxConcurrency` path |
| actual-SEND pacing | HAVE | `SmartPaceController` / `AcquireSendSlotAsync` |
| per-recipient pacing | HAVE | existing recipient gate |
| destination-provider throttling | HAVE | provider windows |
| SMTP session pool | HAVE | `SmtpConnectionPool` |
| multi-account SMTP pools | HAVE | account registry/pool hub |
| endpoint health/quarantine | HAVE | `TransportHealthRegistry` |
| delivery ledger / AutoRestart | HAVE | `DeliveryLedger` |
| connection churn | HAVE | bounded scenario runner |
| target/scenario limits | HAVE | `TargetSet` + `ScenarioLimits` |
| retry policy + budget | HAVE | `RetryPolicy` + `RetryMetrics` |
| unified SMTP outcome classification | HAVE | `SmtpOutcomeClassifier` |
| queue metrics | HAVE | `ScenarioQueueMetrics` |
| RunReport | HAVE | `RunReportBuilder` |
| RunObservability | HAVE | projection from report |
| SMTP/TLS diagnostics | HAVE | `TransportDiagnostics` |
| DNS/MX/SPF/DMARC diagnostics | HAVE | current diagnostics layer |
| endpoint canonicalization/deduplication | HAVE | canonical health keys + target dedup |
| plugin payload architecture | HAVE | `IMailPayloadPlugin` pipeline |
| typed scenario definitions | HAVE | `LoadScenarioKind` + `LoadScenarioDefinition` |
| scenario execution adapter | HAVE | `ScenarioEngine` delegates to `SmtpTestRunner` |
| deterministic provider simulator | FOUNDATION | B4 integration remains next |

## 3. TRACK B status

| Item | Status | Next gate |
|---|---|---|
| B1 canonical documentation reset | COMPLETE | maintain synchronization |
| B2 external mechanism audit | COMPLETE — EXT-AUDIT-001 | audit future repositories as separate items |
| B3 typed scenario engine | COMPLETE | B4 provider simulator integration |
| B4 provider simulator | FOUNDATION | integrate provider events with B3 |
| B5 behavioral analyzer | POST-BASELINE | normalized mail events + deterministic analysis |
| B6 mailbox/deliverability lab | POST-BASELINE | controlled SMTP/IMAP lab boundary |
| B7 richer auth/transport evidence | POST-BASELINE | evidence model over existing diagnostics |
| B8 replayable redacted artifacts | POST-BASELINE | deterministic artifact contract |
| B9 security execution gates | POST-BASELINE | preflight/evidence enforcement |

## 4. B3 delivered behavior

B3 exposes typed scenario definitions while keeping execution in the existing SMTP runner.

Directly executable through `ScenarioEngine`:

- `NormalDelivery`
- `BurstDelivery`
- `SustainedLoad`
- `ConnectionSaturation`
- `ProviderDistribution` (requires at least two configured SMTP accounts/endpoints)
- `Deliverability`

The adapter reuses existing `Channel`, workers, `MaxConcurrency`, `SmartPaceController`, recipient/provider gates, SMTP pools, retry policy and `MailTestResult`.

Simulation-only kinds are represented but deliberately rejected by the direct SMTP adapter until their controlled lab/provider implementation exists:

- `FailureInjection`
- `MailboxQuota`
- `SubscriptionBombSimulation`
- `DoubleOptInSimulation`
- `AntiAbuseControlSimulation`
- `DistributedLab`

This is intentional: a scenario definition must never silently turn a future simulation into real SMTP traffic.

## 5. Security-pattern capability map

| Objective | Current direction | Priority |
|---|---|---|
| subscription-bombing testing | controlled provider/mailbox simulation | P2 |
| Double-Opt-In testing | owned application workflow simulation | P2 |
| CAPTCHA testing | test the control, never bypass it | P2 |
| OTP testing | test the control, never bypass it | P2 |
| automated registrations | synthetic/owned test application only | P2 |
| botnet-like distribution | bounded distributed lab workers only | P2 |
| proxy diversity | controlled lab topology only | P2 |
| provider-limit behavior | observe/test throttling, never evade it | P1/P2 |
| behavioral/anomaly analysis | normalized events + evidence score | P1 |
| smoke-screen detection | correlate burst/diversity/auth/mailbox signals | P2 |
| mailbox saturation | controlled quota/load/recovery scenario | P1 |

A future `YES` means a controlled/authorized testing capability, not unrestricted third-party abuse automation.

## 6. External repository decisions

The EXT-AUDIT-001 retained set is complete and normalized in `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`. Key decisions:

- `rojberr/mailcannon`: REFERENCE only for one-PC load2; no dependency planned.
- `slowhttptest`: HARDEN/ADOPT principles for rate-vs-concurrency, timeout/probe separation and observability; no HTTP reactor port.
- `GoldenEye`: HARDEN/ADAPT session reuse, worker lifecycle and TLS-policy concepts; no attack orchestration port.
- `StruisICT/smtp-test-tool`: REFERENCE/ADOPT diagnostic concepts without a second diagnostics engine.
- `Olib-AI/mailcue`: REFERENCE/SIMULATE/LAB candidate for B6.
- `stalwartlabs/mail-auth`: REFERENCE for mail-auth protocol knowledge.
- `charlesgreen/email`: REFERENCE/ADOPT authentication-evidence analysis concepts.
- `usnistgov/dmarc-tester`: REFERENCE/TEST-FIXTURE source for controlled auth validation.
- Mailpit/MailHog: REFERENCE/LAB for local mail capture.

## 7. Architecture constraints

```text
ScenarioDefinition
    ↓
ScenarioEngine
    ↓
existing bounded Channel
    ↓
existing workers / MaxConcurrency
    ↓
existing SmartPaceController + recipient/provider gates
    ↓
existing SMTP account/session pools
    ↓
existing SendAsync + outcome classification
    ↓
DeliveryLedger / RetryMetrics / RunReport
    ↓
RunObservability + BehavioralAnalysis
```

B4–B9 must not introduce a second queue, pacing/limiting stack, retry policy or source-of-truth result model.

## 8. Security boundary

The framework can represent high-intensity authorized testing, but it must not become a public abuse launcher. Do not implement arbitrary third-party registration automation, CAPTCHA/OTP bypass, anti-abuse evasion, real botnets, provider-limit evasion or unrestricted public-target DoS/DDoS.

Defensive objectives are covered by controlled simulations, owned applications, synthetic providers/recipients and bounded lab workers.

## 9. Rules for future work

- Do not reopen A1–A8 without concrete regression evidence.
- Preserve cancellation, hard limits, DryRun/TestMode, `--unauthorized` and secret redaction.
- Every retry remains under existing pacing/concurrency controls.
- Every new status requires source/test/CI evidence.
- Synchronize canonical documentation after verified implementation changes.
