# A8 — Final baseline (TRACK A)

**Date:** 2026-09-14  
**Authority tip at close:** `d8ff5727c45c4f35bb7d55725a3d2ece79fb0ba5`  
**CI:** [success #240](https://github.com/sirvan0010-alt/load2/actions/runs/34787829729)  
**CodeQL:** [success #125](https://github.com/sirvan0010-alt/load2/actions/runs/34787829727)

## TRACK A status

| ID | Item | Status |
|----|------|--------|
| A1 | Per-target / provider throttling (SmartPace) | ✅ FIXED |
| A2 | Multi-account throttling regression | ✅ FIXED |
| A3 | Bounded scenario queue + queue metrics | ✅ FIXED |
| A4 | Retry policy + budget + RetryMetrics wire | ✅ FIXED |
| A5 | Unified SmtpOutcomeClassifier + OutcomeCounts | ✅ FIXED |
| A6 | Global concurrency audit | ✅ PASS (no arch change) |
| A7 | RunObservability / RunReport projection | ✅ FIXED |
| A8 | Baseline + docs sync | ✅ THIS DOCUMENT |

## Gates verified on main

| Gate | Evidence |
|------|----------|
| Release build + unit tests | CI success on tip |
| CodeQL csharp | success on tip |
| SmtpTestRunner integrity | ~64 KB; no PLACEHOLDER; OutcomeCounts wired |
| DryRun path | workers + queue metrics + outcome Success |
| Cancellation | queue Cancelled/Drained; OCE not retryable |
| `--unauthorized` / TestMode | Validation.Validate gate |
| MaxConcurrency 1–20 | Validation + pool SemaphoreSlim + workers |
| Persistent SMTP sessions | SmtpConnectionPool rent/return |
| Secret-free report JSON | RunReportBuilder.JsonContainsSecretMaterial |

## Architecture invariants (do not break)

1. **One pacing system** — `SmartPaceController` (no second rate limiter).
2. **Bounded workers** — `MaxConcurrency` workers + bounded Channel.
3. **SEND gate** — `AcquireSendSlotAsync` before real `SendAsync` (and retries).
4. **DeliveryLedger** — Accepted is terminal across AutoRestart.
5. **Retry** — `RetryPolicy` → `SmtpOutcomeClassifier.IsRetryable`; global budget.
6. **Outcomes** — single taxonomy for retry, health category, counters, report.
7. **Observability** — `MailTestResult` is source of truth; `RunObservability` is projection only.

## Explicitly out of baseline scope

- Full MainForm redesign / live OutcomeCounters every Progress tick
- Reputation / external monitoring products
- Provider-specific semantics without measured evidence
- Second global concurrency Semaphore beyond workers + pool + pace gate
- Live SMTP/proxy NET-AUDIT against production endpoints (requires user credentials/endpoints)

## How to re-verify

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

GitHub: Actions → CI + CodeQL Advanced on `main`.

## Next (optional, outside TRACK A engine close)

- GUI bind `RunObservability.FromReport` on summary panel
- NET-AUDIT-001 with authorized test endpoints only
- External-repo ADOPT candidates (separate track)
