using Xunit;

namespace MailLoadTester.Tests;

public sealed class AutoRestartMetricsTests
{
    [Fact]
    public void Queue_aggregates_additive_counters_and_keeps_last_depth()
    {
        var first = new ScenarioQueueMetricsSnapshot(2, 8, 10, 7, 6, 3, 1, 2);
        var second = new ScenarioQueueMetricsSnapshot(0, 5, 4, 4, 4, 1, 0, 0);

        var result = AutoRestartMetrics.AggregateQueue(first, second);

        Assert.NotNull(result);
        Assert.Equal(0, result!.QueueDepth);
        Assert.Equal(8, result.PeakQueueDepth);
        Assert.Equal(14, result.Enqueued);
        Assert.Equal(11, result.Dequeued);
        Assert.Equal(10, result.Completed);
        Assert.Equal(4, result.FullWaits);
        Assert.Equal(1, result.Cancelled);
        Assert.Equal(2, result.Drained);
    }

    [Fact]
    public void Retry_aggregates_all_counters_and_attempt_distribution_elementwise()
    {
        var first = new RetryMetricsSnapshot(3, 5, 1, 1, 2, 0, new[] { 0, 3, 2 });
        var second = new RetryMetricsSnapshot(4, 6, 2, 2, 1, 1, new[] { 0, 1, 4, 2 });

        var result = AutoRestartMetrics.AggregateRetry(first, second);

        Assert.NotNull(result);
        Assert.Equal(7, result!.RetryableFailures);
        Assert.Equal(11, result.RetryAttempts);
        Assert.Equal(3, result.SuccessAfterRetry);
        Assert.Equal(3, result.Exhausted);
        Assert.Equal(3, result.PermanentFailures);
        Assert.Equal(1, result.BudgetExhausted);
        Assert.Equal(new[] { 0, 4, 6, 2 }, result.AttemptsByNumber);
    }

    [Fact]
    public void Outcomes_aggregate_each_taxonomy_bucket()
    {
        var first = new SmtpOutcomeCountsSnapshot(90, 3, 1, 1, 0, 0, 2, 2, 0, 1);
        var second = new SmtpOutcomeCountsSnapshot(5, 2, 3, 4, 1, 2, 1, 0, 1, 2);

        var result = AutoRestartMetrics.AggregateOutcomes(first, second);

        Assert.NotNull(result);
        Assert.Equal(95, result!.Success);
        Assert.Equal(5, result.Transient);
        Assert.Equal(4, result.Throttled);
        Assert.Equal(5, result.Timeout);
        Assert.Equal(1, result.Authentication);
        Assert.Equal(2, result.PolicyRejected);
        Assert.Equal(3, result.Recipient);
        Assert.Equal(2, result.Permanent);
        Assert.Equal(1, result.Cancelled);
        Assert.Equal(3, result.Unknown);
    }

    [Fact]
    public void Null_aggregate_preserves_first_snapshot_and_null_next_does_not_erase_it()
    {
        var queue = new ScenarioQueueMetricsSnapshot(0, 2, 3, 3, 3, 0, 0, 0);
        var retry = new RetryMetricsSnapshot(1, 1, 0, 0, 0, 0, new[] { 0, 1 });
        var outcomes = new SmtpOutcomeCountsSnapshot(3, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.Same(queue, AutoRestartMetrics.AggregateQueue(null, queue));
        Assert.Same(queue, AutoRestartMetrics.AggregateQueue(queue, null));
        Assert.Same(retry, AutoRestartMetrics.AggregateRetry(null, retry));
        Assert.Same(retry, AutoRestartMetrics.AggregateRetry(retry, null));
        Assert.Same(outcomes, AutoRestartMetrics.AggregateOutcomes(null, outcomes));
        Assert.Same(outcomes, AutoRestartMetrics.AggregateOutcomes(outcomes, null));
    }
}
