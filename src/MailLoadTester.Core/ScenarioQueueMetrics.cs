namespace MailLoadTester;

/// <summary>
/// Thread-safe counters for the existing bounded scenario Channel pipeline.
/// This is instrumentation only; it is not a rate limiter and does not control pacing.
/// </summary>
public sealed class ScenarioQueueMetrics
{
    long _depth;
    long _peakDepth;
    long _enqueued;
    long _dequeued;
    long _completed;
    long _fullWaits;
    long _cancelled;
    long _drained;

    public long QueueDepth => Math.Max(0, Volatile.Read(ref _depth));
    public long PeakQueueDepth => Math.Max(0, Volatile.Read(ref _peakDepth));
    public long Enqueued => Math.Max(0, Volatile.Read(ref _enqueued));
    public long Dequeued => Math.Max(0, Volatile.Read(ref _dequeued));
    public long Completed => Math.Max(0, Volatile.Read(ref _completed));
    public long FullWaits => Math.Max(0, Volatile.Read(ref _fullWaits));
    public long Cancelled => Math.Max(0, Volatile.Read(ref _cancelled));
    public long Drained => Math.Max(0, Volatile.Read(ref _drained));

    public void RecordEnqueued()
    {
        Interlocked.Increment(ref _enqueued);
        var depth = Interlocked.Increment(ref _depth);
        UpdatePeak(depth);
    }

    public void RecordFullWait() => Interlocked.Increment(ref _fullWaits);

    public void RecordDequeued()
    {
        Interlocked.Increment(ref _dequeued);
        DecrementDepth();
    }

    public void RecordCompleted() => Interlocked.Increment(ref _completed);

    public void RecordCancelled() => Interlocked.Increment(ref _cancelled);

    /// <summary>Records an item removed from the queue without being processed.</summary>
    public void RecordDrained()
    {
        Interlocked.Increment(ref _drained);
        DecrementDepth();
    }

    public ScenarioQueueMetricsSnapshot Snapshot() => new(
        QueueDepth,
        PeakQueueDepth,
        Enqueued,
        Dequeued,
        Completed,
        FullWaits,
        Cancelled,
        Drained);

    void DecrementDepth()
    {
        while (true)
        {
            var current = Volatile.Read(ref _depth);
            if (current <= 0) return;
            if (Interlocked.CompareExchange(ref _depth, current - 1, current) == current)
                return;
        }
    }

    void UpdatePeak(long depth)
    {
        while (true)
        {
            var peak = Volatile.Read(ref _peakDepth);
            if (depth <= peak) return;
            if (Interlocked.CompareExchange(ref _peakDepth, depth, peak) == peak)
                return;
        }
    }
}

public sealed record ScenarioQueueMetricsSnapshot(
    long QueueDepth,
    long PeakQueueDepth,
    long Enqueued,
    long Dequeued,
    long Completed,
    long FullWaits,
    long Cancelled,
    long Drained);
