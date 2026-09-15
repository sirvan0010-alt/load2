# load2 — Current Capability and Gap Matrix

**Authority:** `sirvan0010-alt/load2` / `main`  
**Baseline:** TRACK A A1–A8 ALL COMPLETE.  
**Current evolution:** TRACK B post-baseline.

## 0. Status rules

`HAVE` means demonstrated in current source/tests. `FOUNDATION` means the contract exists but full engine integration is not complete. `POST-BASELINE` means planned work.

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

## 3. TRACK B — current state

| Item | Status | Next step |
|---|---|---|
| B1 canonical documentation reset | FOUNDATION | keep canonical docs synchronized; migrate active docs |
| B2 external mechanism audit | IN PROGRESS | pin/source-audit retained candidates |
| B3 typed scenario model | FOUNDATION | integrate definitions with existing runner |
| B4 deterministic provider simulator | FOUNDATION | compose simulators with B3 and event evidence |
| B5 behavioral analyzer | POST-BASELINE | normalize simulator/transport/mailbox events |
| B6 mailbox/deliverability lab | POST-BASELINE | controlled SMTP/IMAP environment |
| B7 richer auth/transport evidence | POST-BASELINE | extend evidence without duplicate diagnostics |
| B8 replayable redacted artifacts | POST-BASELINE | deterministic scenario replay |
| B9 security execution gates | POST-BASELINE | enforce preflight/evidence gate chain |

## 4. Security-pattern capability map

| Objective | load2 direction | Priority |
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

## 5. External repository decisions

### `rojberr/mailcannon`

**Decision: REFERENCE.** Its distributed Docker/Swarm/Kubernetes scaling model is not a current dependency for a one-PC load2 deployment. load2 already has bounded local execution. Revisit only if a concrete single-host/reproducibility benefit is demonstrated.

### Mail-security lab references

Controlled mail-server/testing projects are environment references for B6/B7. They remain outside the core SMTP transport layer.

## 6. Architecture constraints for B3/B4/B5

```text
ScenarioDefinition
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

B3/B4/B5 must not introduce a second queue, pacing stack, retry policy or source-of-truth result model.

## 7. Security boundary

The framework can represent high-intensity authorized testing, but it must not become a public abuse launcher. Do not implement arbitrary third-party registration automation, CAPTCHA/OTP bypass, anti-abuse evasion, real botnets, provider-limit evasion or unrestricted public-target DoS/DDoS.

The defensive objectives are covered by simulation, owned applications, synthetic providers/recipients and bounded lab execution.

## 8. Rules for future work

- Do not reopen A1–A8 without concrete regression evidence.
- Preserve cancellation, hard limits, DryRun/TestMode, `--unauthorized` and secret redaction.
- Keep `MailTestResult` as the source of truth.
- Every retry must remain under existing pacing/concurrency controls.
- Every new status requires source/test/CI evidence.
- Synchronize canonical documentation after verified implementation changes.
