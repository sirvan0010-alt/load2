namespace MailLoadTester;

/// <summary>
/// A7: single flat view for CLI/GUI/dashboard derived from <see cref="MailTestResult"/>.
/// Not a parallel telemetry system — only projects fields already on the result/report.
/// </summary>
public sealed record RunObservabilitySnapshot(
    string? RunId,
    int Requested,
    int Sent,
    int Failed,
    bool Cancelled,
    double SuccessRatePct,
    double ThroughputPerSec,
    double ActiveThroughputPerSec,
    double AvgLatencyMs,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double AvgPrepWaitMs,
    double AvgAdaptiveWaitMs,
    double AvgPoolWaitMs,
    double AvgPaceWaitMs,
    double AvgSmtpSendMs,
    int Retries,
    int Smtp4xx,
    int Smtp5xx,
    int Timeouts,
    int PoolConnections,
    int AdaptiveConcurrency,
    bool CircuitBreakerOpen,
    int AutoRestartAttempts,
    string LastError,
    ScenarioQueueMetricsSnapshot? Queue,
    RetryMetricsSnapshot? Retry,
    SmtpOutcomeCountsSnapshot? Outcomes,
    IReadOnlyList<EndpointHealthSnapshot>? EndpointHealth);

/// <summary>A7 projection helpers. GUI/CLI should prefer this over ad-hoc field picking.</summary>
public static class RunObservability
{
    public static RunObservabilitySnapshot FromResult(MailTestResult r)
    {
        var attempted = r.Sent + r.Failed;
        var successRate = attempted > 0 ? 100.0 * r.Sent / attempted : 0.0;
        return new RunObservabilitySnapshot(
            RunId: r.RunId,
            Requested: r.Requested,
            Sent: r.Sent,
            Failed: r.Failed,
            Cancelled: r.Cancelled,
            SuccessRatePct: successRate,
            ThroughputPerSec: r.ThroughputPerSec,
            ActiveThroughputPerSec: r.ActiveThroughputPerSec,
            AvgLatencyMs: r.AvgLatencyMs,
            P50LatencyMs: r.P50LatencyMs,
            P95LatencyMs: r.P95LatencyMs,
            P99LatencyMs: r.P99LatencyMs,
            AvgPrepWaitMs: r.AvgPrepWaitMs,
            AvgAdaptiveWaitMs: r.AvgAdaptiveWaitMs,
            AvgPoolWaitMs: r.AvgPoolWaitMs,
            AvgPaceWaitMs: r.AvgPaceWaitMs,
            AvgSmtpSendMs: r.AvgSmtpSendMs,
            Retries: r.Retries,
            Smtp4xx: r.Smtp4xx,
            Smtp5xx: r.Smtp5xx,
            Timeouts: r.Timeouts,
            PoolConnections: r.PoolConnections,
            AdaptiveConcurrency: r.AdaptiveConcurrency,
            CircuitBreakerOpen: r.CircuitBreakerOpen,
            AutoRestartAttempts: r.AutoRestartAttempts,
            LastError: r.LastError ?? "",
            Queue: r.QueueMetrics,
            Retry: r.RetryMetrics,
            Outcomes: r.OutcomeCounts,
            EndpointHealth: r.EndpointHealth);
    }

    public static RunObservabilitySnapshot FromReport(RunReport report)
    {
        var snap = FromResult(report.Result);
        // Prefer report-level RunId / ledger counts when they differ (AutoRestart aggregate).
        return snap with
        {
            RunId = report.RunId,
            Sent = report.LedgerAccepted,
            Failed = report.LedgerFailed,
            SuccessRatePct = (report.LedgerAccepted + report.LedgerFailed) > 0
                ? 100.0 * report.LedgerAccepted / (report.LedgerAccepted + report.LedgerFailed)
                : snap.SuccessRatePct,
            EndpointHealth = report.EndpointHealth
        };
    }

    /// <summary>One-line status for logs / dashboard footer — no secrets.</summary>
    public static string FormatSummaryLine(RunObservabilitySnapshot s)
    {
        var q = s.Queue is { } qm
            ? $" queue peak={qm.PeakQueueDepth} fullWaits={qm.FullWaits}"
            : "";
        var o = s.Outcomes is { } oc
            ? $" outcomes T/R/P={oc.Transient}/{oc.Throttled}/{oc.Permanent}"
            : "";
        return
            $"run={s.RunId ?? "-"} sent={s.Sent}/{s.Requested} fail={s.Failed} " +
            $"ok={s.SuccessRatePct:0.0}% tput={s.ThroughputPerSec:0.00}/s " +
            $"retries={s.Retries} 4xx={s.Smtp4xx} 5xx={s.Smtp5xx} to={s.Timeouts}" +
            q + o +
            (s.Cancelled ? " CANCELLED" : "");
    }
}
