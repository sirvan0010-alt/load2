namespace MailLoadTester;

/// <summary>
/// Fixed-bucket latency distribution built from the same successful-message
/// latency samples already collected by <see cref="SmtpTestRunner"/>.
/// This is a bounded projection, not a second unbounded sample store.
/// </summary>
public sealed class LatencyHistogram
{
    // Lower bounds in milliseconds. Each bucket is [lower, nextLower), with
    // the final bucket covering [100000, +infinity).
    public static readonly double[] BucketLowerBounds =
    {
        0, 1, 5, 10, 25, 50, 100, 250, 500, 1_000,
        2_500, 5_000, 10_000, 25_000, 50_000, 100_000
    };

    static readonly string[] BucketLabels =
    {
        "0-1ms", "1-5ms", "5-10ms", "10-25ms", "25-50ms", "50-100ms",
        "100-250ms", "250-500ms", "500-1000ms", "1000-2500ms", "2500-5000ms",
        "5000-10000ms", "10000-25000ms", "25000-50000ms", "50000-100000ms", "100000ms+"
    };

    readonly long[] _counts = new long[BucketLowerBounds.Length];

    public void Add(double milliseconds)
    {
        if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds))
            return;

        if (milliseconds < 0)
            milliseconds = 0;

        var index = FindBucket(milliseconds);
        Interlocked.Increment(ref _counts[index]);
    }

    public void AddRange(IEnumerable<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        foreach (var sample in samples)
            Add(sample);
    }

    public LatencyHistogramSnapshot Snapshot()
    {
        var buckets = new LatencyHistogramBucket[_counts.Length];
        for (var i = 0; i < _counts.Length; i++)
            buckets[i] = new LatencyHistogramBucket(BucketLabels[i], BucketLowerBounds[i], i + 1 < BucketLowerBounds.Length ? BucketLowerBounds[i + 1] : null, Interlocked.Read(ref _counts[i]));

        return new LatencyHistogramSnapshot(buckets);
    }

    public static LatencyHistogramSnapshot Merge(
        LatencyHistogramSnapshot? aggregate,
        LatencyHistogramSnapshot? next)
    {
        if (next is null) return aggregate ?? EmptySnapshot();
        if (aggregate is null) return next;

        if (aggregate.Buckets.Count != BucketLowerBounds.Length || next.Buckets.Count != BucketLowerBounds.Length)
            throw new ArgumentException("Latency histogram snapshots mají nekompatibilní počet bucketů.");

        var buckets = new LatencyHistogramBucket[BucketLowerBounds.Length];
        for (var i = 0; i < buckets.Length; i++)
        {
            var left = aggregate.Buckets[i];
            var right = next.Buckets[i];
            buckets[i] = new LatencyHistogramBucket(
                BucketLabels[i],
                BucketLowerBounds[i],
                i + 1 < BucketLowerBounds.Length ? BucketLowerBounds[i + 1] : null,
                SaturatingAdd(left.Count, right.Count));
        }

        return new LatencyHistogramSnapshot(buckets);
    }

    public static LatencyHistogramSnapshot FromSamples(IEnumerable<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var histogram = new LatencyHistogram();
        histogram.AddRange(samples);
        return histogram.Snapshot();
    }

    static LatencyHistogramSnapshot EmptySnapshot()
        => new(BucketLowerBounds.Select((lower, i) =>
            new LatencyHistogramBucket(
                BucketLabels[i],
                lower,
                i + 1 < BucketLowerBounds.Length ? BucketLowerBounds[i + 1] : null,
                0)).ToArray());

    static int FindBucket(double milliseconds)
    {
        var lo = 0;
        var hi = BucketLowerBounds.Length - 1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) / 2);
            if (BucketLowerBounds[mid] <= milliseconds)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return Math.Max(0, hi);
    }

    static long SaturatingAdd(long left, long right)
        => left > long.MaxValue - right ? long.MaxValue : left + right;
}

public sealed record LatencyHistogramBucket(
    string Label,
    double LowerBoundMs,
    double? UpperBoundMs,
    long Count);

public sealed record LatencyHistogramSnapshot(
    IReadOnlyList<LatencyHistogramBucket> Buckets)
{
    public long TotalCount => Buckets.Sum(x => x.Count);
}
