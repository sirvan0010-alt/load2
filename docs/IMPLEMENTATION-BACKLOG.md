# load2 — SMTP-first Implementation Backlog

**Authority:** source + tests on `main`.

## TRACK A — closed (A1–A8)

| Item | Status |
|------|--------|
| Health / Report / Multi-account / Churn / Provider / Diagnostics | ✅ |
| TargetSet / ScenarioLimits / DurationSeconds | ✅ |
| GUI TransportDiagnostics / Accounts editor | ✅ |
| **A1** destination-provider throttling | ✅ FIXED |
| **A2** multi-account throttling regression | ✅ FIXED |
| **A3** bounded scenario queue + metrics | ✅ FIXED |
| **A4** retry / budget + RetryMetrics | ✅ FIXED |
| **A5** SmtpOutcomeClassifier + OutcomeCounts | ✅ FIXED |
| **A6** global concurrency audit | ✅ PASS (no change) |
| **A7** RunObservability / RunReport projection | ✅ FIXED |
| **A8** final baseline | ✅ see `docs/A8-BASELINE.md` |

```text
A1 → A2 → A3 → A4 → A5 → A6 → A7 → A8   ALL COMPLETE
```

## Invariants (engine)

- One SmartPace system; bounded workers = MaxConcurrency
- AcquireSendSlotAsync before every real SEND (incl. retries)
- DeliveryLedger Accepted terminal
- RetryPolicy delegates to SmtpOutcomeClassifier
- MailTestResult → RunReport → RunObservability (no parallel metrics pipeline)

## Parallel track — external repository audit

External audits remain independent of TRACK A.  
ADOPT only with: mechanism → source evidence → load2 mapping → decision.

## Optional post-baseline

- GUI summary panel: `RunObservability.FromReport`
- NET-AUDIT-001 on authorized endpoints only
- Release packaging / version bump (product decision)
