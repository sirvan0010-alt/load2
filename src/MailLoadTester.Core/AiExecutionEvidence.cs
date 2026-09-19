namespace MailLoadTester;

/// <summary>
/// Structured, secret-free evidence produced by the AI execution boundary.
/// It projects existing MailTestResult metrics; it is not a second telemetry store.
/// </summary>
public sealed record AiExecutionEvidence(
    string ActionId,
    string AgentId,
    string Status,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    int Requested,
    int Sent,
    int Failed,
    bool Cancelled,
    double SuccessRatePct,
    double ThroughputPerSec,
    double P95LatencyMs,
    int Retries,
    int Smtp4xx,
    int Smtp5xx,
    int Timeouts,
    bool CircuitBreakerOpen);

public sealed record AiExecutionVerification(
    bool Passed,
    IReadOnlyList<string> Findings);

/// <summary>
/// Mechanical post-execution verification. It never changes the result and never
/// grants additional authority to the AI planner.
/// </summary>
public static class AiExecutionVerifier
{
    public static AiExecutionVerification Verify(
        AiAction action,
        MailTestResult result)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(result);

        var findings = new List<string>();

        if (result.Requested > action.MaxMessages)
            findings.Add("Result requested count exceeded the authorized action budget.");

        if (result.Sent > action.MaxMessages)
            findings.Add("Result sent count exceeded the authorized action budget.");

        if (result.Cancelled)
            findings.Add("Execution was cancelled.");

        if (result.CircuitBreakerOpen)
            findings.Add("Circuit breaker opened during execution.");

        return new AiExecutionVerification(
            Passed: findings.Count == 0,
            Findings: findings);
    }

    public static AiExecutionEvidence FromResult(
        AiAction action,
        MailTestResult result,
        DateTimeOffset startedUtc,
        DateTimeOffset completedUtc)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(result);

        var attempted = result.Sent + result.Failed;
        var successRate = attempted > 0
            ? 100.0 * result.Sent / attempted
            : 0.0;

        var status = result.Cancelled
            ? "cancelled"
            : result.Failed > 0 || result.CircuitBreakerOpen
                ? "completed-with-failures"
                : "completed";

        return new AiExecutionEvidence(
            ActionId: action.ActionId,
            AgentId: action.AgentId,
            Status: status,
            StartedUtc: startedUtc,
            CompletedUtc: completedUtc,
            Requested: result.Requested,
            Sent: result.Sent,
            Failed: result.Failed,
            Cancelled: result.Cancelled,
            SuccessRatePct: successRate,
            ThroughputPerSec: result.ThroughputPerSec,
            P95LatencyMs: result.P95LatencyMs,
            Retries: result.Retries,
            Smtp4xx: result.Smtp4xx,
            Smtp5xx: result.Smtp5xx,
            Timeouts: result.Timeouts,
            CircuitBreakerOpen: result.CircuitBreakerOpen);
    }
}
