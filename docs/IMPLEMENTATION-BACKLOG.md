# load2 — SMTP-first Implementation Backlog

**Authority:** source + tests on `main`.

## Verified done

| Item | Status |
|------|--------|
| Health / Report / Multi-account / Churn / Provider / Diagnostics | ✅ |
| TargetSet / ScenarioLimits / DurationSeconds | ✅ |
| GUI TransportDiagnostics on Test connection | ✅ |
| GUI Accounts editor | ✅ |
| A1 destination-provider throttling | ✅ `175b379` CI #208 |
| A2 multi-account throttling regression | ✅ `b3e075f` CI #210 |
| A3 bounded scenario queue + metrics | ✅ queue metrics wired; CI green on prior tip |

## Current execution plan

```text
A1 destination-provider throttling                 ✅ VERIFIED
A2 multi-account throttling regression + metrics   ✅ FIXED
A3 bounded scenario queue + queue metrics          ✅ FIXED
A4 retry / requeue with hard limits + retry metrics ← CURRENT
A5 unified SMTP response classification + counters
A6 global concurrency audit                         CONDITIONAL
A7 final observability/report/GUI reconciliation
A8 verification / documentation / release baseline
```

## A4 — Retry / requeue with hard limits

**Status: CURRENT (foundation on main)**

### Already on main

- `RetryPolicy.cs` — IsRetryable (4xx/timeout/protocol), GetDelay, GlobalBudget
- `RetryMetrics.cs` + `RetryMetricsSnapshot` — attempts histogram, exhausted, success-after-retry, budget
- Unit tests: `RetryPolicyTests` / `RetryMetricsTests`
- Docs: `docs/a4-runner.diff`, `docs/a4-models.diff`, `docs/A4-APPLY.md`

### Still required for FIXED

1. Apply Models property `RetryMetrics` (`docs/a4-models.diff`)
2. Apply `docs/a4-runner.diff` to `SmtpTestRunner.cs`
3. CI green

### Behaviour after wire

- Per-message `MaxRetries` unchanged
- Global budget = `MaxRetries * MessageCount`
- Connection discarded **before** backoff (no lease held during delay)
- Each attempt still goes through SmartPace SEND gate
- No second rate-limiter; full classifier deferred to A5

### Acceptance

- unit tests green
- wire + CI green
- permanent 5xx not retried
- cancel during delay does not hang

Do not start A5 until A4 is FIXED on main with green CI.

## Parallel track — external repository audit

External audits continue independently from the engine backlog.
