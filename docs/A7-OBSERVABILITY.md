# A7 — Observability unification

**Status: IMPLEMENTED (projection layer)**

## Contract

```text
SmtpTestRunner
    → MailTestResult   (source of truth: timing, queue, retry, outcomes, health)
        → RunReportBuilder.Create  (secret-free options + ledger + timestamps)
            → RunObservability.FromResult / FromReport  (flat GUI/CLI view)
                → FormatSummaryLine / JSON via RunReportBuilder.ToJson
```

ProgressUpdate remains the **live** stream (per-message path).
MailTestResult / RunReport / RunObservabilitySnapshot are the **end-of-run** contract.

## Included in snapshot

| Group | Fields |
|-------|--------|
| Delivery | Requested, Sent, Failed, SuccessRatePct, Cancelled |
| Throughput | ThroughputPerSec, ActiveThroughputPerSec |
| Latency | Avg, P50, P95, P99 |
| Phase waits (FEAT-022) | Prep, Adaptive, Pool, Pace, SmtpSend |
| SMTP aggregates | Retries, 4xx, 5xx, Timeouts |
| Engine | PoolConnections, AdaptiveConcurrency, CircuitBreakerOpen |
| A3 Queue | QueueMetrics snapshot |
| A4 Retry | RetryMetrics snapshot |
| A5 Outcomes | OutcomeCounts snapshot |
| Health | EndpointHealth |
| Identity | RunId, LastError |

## GUI usage

After `RunAsync`:

```csharp
var result = await runner.RunAsync(options, progress, ct);
var report = RunReportBuilder.Create(result.RunId ?? Guid.NewGuid().ToString("N"),
    startedUtc, DateTimeOffset.UtcNow, options, result);
var view = RunObservability.FromReport(report);
// bind view.* to summary panel; persist RunReportBuilder.ToJson(report)
```

Do **not** invent a second metrics pipeline in the GUI.

## Out of scope for A7

- Rewriting MainForm layout (optional later)
- Live ProgressUpdate carrying full OutcomeCounters every tick (noise under load)
- Reputation / external monitoring
