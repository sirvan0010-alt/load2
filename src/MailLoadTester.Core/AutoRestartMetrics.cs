namespace MailLoadTester;

/// <summary>
/// Aggregates per-attempt observability for an AutoRestart run.
/// Delivery counts remain owned by DeliveryLedger; these methods only combine
/// additive attempt telemetry and preserve the final queue depth from the last attempt.
/// </summary>
public static class AutoRestartMetrics
{
    public static ScenarioQueueMetricsSnapshot? AggregateQueue(
        ScenarioQueueMetricsSnapshot? aggregate,
        ScenarioQueueMetricsSnapshot? next)
    {
        if (next is null) return aggregate;
        if (aggregate is null) return next;

        return new ScenarioQueueMetricsSnapshot(
            QueueDepth: next.QueueDepth,
            PeakQueueDepth: Math.Max(aggregate.PeakQueueDepth, next.PeakQueueDepth),
            Enqueued: SaturatingAdd(aggregate.Enqueued, next.Enqueued),
            Dequeued: SaturatingAdd(aggregate.Dequeued, next.Dequeued),
            Completed: SaturatingAdd(aggregate.Completed, next.Completed),
            FullWaits: SaturatingAdd(aggregate.FullWaits, next.FullWaits),
            Cancelled: SaturatingAdd(aggregate.Cancelled, next.Cancelled),
            Drained: SaturatingAdd(aggregate.Drained, next.Drained));
    }

    public static RetryMetricsSnapshot? AggregateRetry(
        RetryMetricsSnapshot? aggregate,
        RetryMetricsSnapshot? next)
    {
        if (next is null) return aggregate;
        if (aggregate is null) return next;

        var length = Math.Max(aggregate.AttemptsByNumber.Count, next.AttemptsByNumber.Count);
        var attempts = new int[length];
        for (var i = 0; i < length; i++)
        {
            var left = i < aggregate.AttemptsByNumber.Count ? aggregate.AttemptsByNumber[i] : 0;
            var right = i < next.AttemptsByNumber.Count ? next.AttemptsByNumber[i] : 0;
            attempts[i] = SaturatingAdd(left, right);
        }

        return new RetryMetricsSnapshot(
            RetryableFailures: SaturatingAdd(aggregate.RetryableFailures, next.RetryableFailures),
            RetryAttempts: SaturatingAdd(aggregate.RetryAttempts, next.RetryAttempts),
            SuccessAfterRetry: SaturatingAdd(aggregate.SuccessAfterRetry, next.SuccessAfterRetry),
            Exhausted: SaturatingAdd(aggregate.Exhausted, next.Exhausted),
            PermanentFailures: SaturatingAdd(aggregate.PermanentFailures, next.PermanentFailures),
            BudgetExhausted: SaturatingAdd(aggregate.BudgetExhausted, next.BudgetExhausted),
            AttemptsByNumber: attempts);
    }

    public static SmtpOutcomeCountsSnapshot? AggregateOutcomes(
        SmtpOutcomeCountsSnapshot? aggregate,
        SmtpOutcomeCountsSnapshot? next)
    {
        if (next is null) return aggregate;
        if (aggregate is null) return next;

        return new SmtpOutcomeCountsSnapshot(
            Success: SaturatingAdd(aggregate.Success, next.Success),
            Transient: SaturatingAdd(aggregate.Transient, next.Transient),
            Throttled: SaturatingAdd(aggregate.Throttled, next.Throttled),
            Timeout: SaturatingAdd(aggregate.Timeout, next.Timeout),
            Authentication: SaturatingAdd(aggregate.Authentication, next.Authentication),
            PolicyRejected: SaturatingAdd(aggregate.PolicyRejected, next.PolicyRejected),
            Recipient: SaturatingAdd(aggregate.Recipient, next.Recipient),
            Permanent: SaturatingAdd(aggregate.Permanent, next.Permanent),
            Cancelled: SaturatingAdd(aggregate.Cancelled, next.Cancelled),
            Unknown: SaturatingAdd(aggregate.Unknown, next.Unknown));
    }

    static int SaturatingAdd(int left, int right)
        => left > int.MaxValue - right ? int.MaxValue : left + right;

    static long SaturatingAdd(long left, long right)
        => left > long.MaxValue - right ? long.MaxValue : left + right;
}
