using System.Collections.Concurrent;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class ScenarioQueueMetricsTests
{
    [Fact]
    public void EnqueueDequeueCompleted_TracksDepthAndPeak()
    {
        var metrics = new ScenarioQueueMetrics();

        metrics.RecordEnqueued();
        metrics.RecordEnqueued();
        metrics.RecordDequeued();
        metrics.RecordCompleted();

        var snapshot = metrics.Snapshot();

        Assert.Equal(1, snapshot.QueueDepth);
        Assert.Equal(2, snapshot.PeakQueueDepth);
        Assert.Equal(2, snapshot.Enqueued);
        Assert.Equal(1, snapshot.Dequeued);
        Assert.Equal(1, snapshot.Completed);
        Assert.Equal(0, snapshot.FullWaits);
        Assert.Equal(0, snapshot.Cancelled);
        Assert.Equal(0, snapshot.Drained);
    }

    [Fact]
    public void FullWaitCancelledAndDrained_AreSeparateOutcomes()
    {
        var metrics = new ScenarioQueueMetrics();

        metrics.RecordEnqueued();
        metrics.RecordFullWait();
        metrics.RecordCancelled();
        metrics.RecordDrained();

        var snapshot = metrics.Snapshot();

        Assert.Equal(0, snapshot.QueueDepth);
        Assert.Equal(1, snapshot.PeakQueueDepth);
        Assert.Equal(1, snapshot.Enqueued);
        Assert.Equal(0, snapshot.Dequeued);
        Assert.Equal(0, snapshot.Completed);
        Assert.Equal(1, snapshot.FullWaits);
        Assert.Equal(1, snapshot.Cancelled);
        Assert.Equal(1, snapshot.Drained);
    }

    [Fact]
    public async Task ConcurrentProducersAndConsumers_DoNotCorruptDepthOrPeak()
    {
        var metrics = new ScenarioQueueMetrics();
        const int count = 10_000;

        var producers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < count / 4; i++)
                    metrics.RecordEnqueued();
            }))
            .ToArray();

        await Task.WhenAll(producers);

        var consumers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < count / 4; i++)
                    metrics.RecordDequeued();
            }))
            .ToArray();

        await Task.WhenAll(consumers);

        var snapshot = metrics.Snapshot();

        Assert.Equal(0, snapshot.QueueDepth);
        Assert.Equal(count, snapshot.Enqueued);
        Assert.Equal(count, snapshot.Dequeued);
        Assert.True(snapshot.PeakQueueDepth > 0);
        Assert.Equal(count, snapshot.PeakQueueDepth);
    }
}
