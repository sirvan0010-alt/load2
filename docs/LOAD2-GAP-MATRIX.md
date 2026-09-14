# load2 — External mechanism gap matrix

**Authority:** `sirvan0010-alt/load2` / `main`  
**Current baseline:** TRACK A A1–A8 **ALL COMPLETE**.

## 0. Interpretation rule

External repositories are audited **by mechanism**, not by project label. A repository named bomber, flooder, scanner, POC, stress tool or similar is not automatically rejected.

For each mechanism, distinguish reusable engineering from an abuse-specific execution path. Reusable scheduling, concurrency, pacing, retry, provider health, session lifecycle, diagnostics and observability may be adopted/adapted when they improve authorized load2 testing.

Decision tags:

`HAVE | GAP | ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`

`REJECT` applies to the mechanism, not automatically to an entire repository.

## 1. TRACK A status

| Area | Current status |
|---|---|
| A1 destination-provider throttling | ✅ FIXED |
| A2 multi-account throttling regression | ✅ FIXED |
| A3 bounded scenario queue + metrics | ✅ FIXED |
| A4 retry policy + budget + RetryMetrics | ✅ FIXED |
| A5 unified SMTP outcome classification | ✅ FIXED |
| A6 global concurrency audit | ✅ PASS — no architecture change |
| A7 RunObservability / RunReport | ✅ FIXED |
| A8 final baseline/documentation | ✅ COMPLETE |
| FEAT-022 phase timing | ✅ IMPLEMENTED |

See `docs/IMPLEMENTATION-BACKLOG.md` and `docs/A8-BASELINE.md` for the authoritative baseline record.

## 2. Current capability map

| Mechanism | load2 status | Current evidence / treatment |
|---|---|---|
| bounded worker queue | HAVE | bounded `Channel<T>` + fixed workers |
| adaptive concurrency | HAVE | bounded by `MaxConcurrency` |
| actual-SEND pacing | HAVE | `SmartPaceController` / `AcquireSendSlotAsync` |
| per-recipient pacing | HAVE | existing recipient gate/limiter |
| destination-provider throttling | HAVE | SmartPace provider windows |
| SMTP session pool | HAVE | persistent `SmtpConnectionPool` |
| multi-account SMTP pools | HAVE | `SmtpAccountRegistry` / `SmtpAccountPoolHub` |
| endpoint health/quarantine | HAVE | `TransportHealthRegistry` |
| delivery ledger / AutoRestart | HAVE | `DeliveryLedger` |
| connection churn | HAVE | bounded scenario runner |
| scenario model / target sets | HAVE | `TargetSet` + `ScenarioLimits` |
| retry policy + retry budget | HAVE | `RetryPolicy` + `RetryMetrics` |
| unified SMTP outcome classification | HAVE | `SmtpOutcomeClassifier` + `OutcomeCounts` |
| queue metrics | HAVE | `ScenarioQueueMetrics` |
| run report | HAVE | `RunReportBuilder` |
| run observability projection | HAVE | `RunObservability` |
| FEAT-022 timing breakdown | HAVE | phase timing fields on result/report |
| DNS/MX/SPF/DMARC diagnostics | HAVE | read-only diagnostics layer where implemented |
| SMTP/TLS diagnostics | HAVE | `TransportDiagnostics` |
| provider/transport registry | PARTIAL / POST-BASELINE | evaluate external candidates against existing abstractions before extending |
| endpoint canonicalization/deduplication | POST-BASELINE | only add if source-level gap is proven |
| replayable run artifacts | POST-BASELINE | extend only with concrete product requirement |
| failure injection | POST-BASELINE | controlled/lab scenarios only |
| GUI summary binding | POST-BASELINE | use `RunObservability.FromReport` |
| live NET-AUDIT-001 | POST-BASELINE | authorized endpoints/fixtures only |

## 3. External audit

The retained external repositories are handled independently from TRACK A. Their useful mechanisms are recorded in `docs/external-repos/*.md` and summarized in `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`.

Audit method:

```text
source audit
  → mechanism inventory
  → load2 comparison
  → decision tag
  → focused implementation only when justified
  → tests
  → CI / CodeQL
  → documentation sync
```

README-only claims are not implementation evidence. Unknowns remain explicitly unknown.

## 4. Authorized stress-testing boundary

The framework may implement controlled SMTP/application stress mechanisms such as repeated-send scenarios, rate/concurrency tests, connection churn, retry/failure stress and multi-target scenarios when targets and scope are explicitly authorized and existing controls remain intact.

Network transport stress may be represented through bounded lab/authorized scenarios. A distributed authorized test is not automatically equivalent to an attack.

Do not import credential/token theft, CAPTCHA/OTP bypass, stealth/evasion for abuse, arbitrary public-target discovery for flooding, provider-abuse bypass, reflection/amplification or unrestricted destructive DoS/DDoS launchers.

## 5. Rules for future work

- Do not reopen A1–A8 as unfinished without concrete regression evidence.
- Do not add a second pacing/limiting stack when the existing `SmartPaceController` covers the contract.
- Do not add a second queue when the bounded scenario `Channel` covers the contract.
- Retry paths must use the existing pacing/concurrency controls.
- Keep `MailTestResult` as the source of truth and `RunReport`/`RunObservability` as projections.
- Preserve cancellation, hard limits, DryRun/TestMode and `--unauthorized` behavior.
- Never introduce secrets into source, logs or reports.
