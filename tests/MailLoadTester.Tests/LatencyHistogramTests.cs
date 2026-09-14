using System.Collections.Concurrent;
using System.Text.Json;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class LatencyHistogramTests
{
    [Fact]
    public void Add_uses_lower_bound_inclusive_and_upper_bound_exclusive_buckets()
    {
        var histogram = new LatencyHistogram();
        histogram.Add(0);
        histogram.Add(1);
        histogram.Add(5);
        histogram.Add(10);
        histogram.Add(25);
        histogram.Add(50);
        histogram.Add(100);
        histogram.Add(250);
        histogram.Add(500);
        histogram.Add(1_000);
        histogram.Add(2_500);
        histogram.Add(5_000);
        histogram.Add(10_000);
        histogram.Add(25_000);
        histogram.Add(50_000);
        histogram.Add(100_000);
        histogram.Add(250_000);

        var buckets = histogram.Snapshot().Buckets;

        Assert.All(buckets.Take(15), b => Assert.Equal(1, b.Count));
        Assert.Equal(2, buckets[15].Count);
        Assert.Equal(17, buckets.Sum(b => b.Count));
    }

    [Fact]
    public void Invalid_values_are_ignored_and_negative_values_are_clamped_to_zero_bucket()
    {
        var histogram = new LatencyHistogram();
        histogram.Add(-10);
        histogram.Add(double.NaN);
        histogram.Add(double.PositiveInfinity);
        histogram.Add(double.NegativeInfinity);

        var snapshot = histogram.Snapshot();

        Assert.Equal(1, snapshot.TotalCount);
        Assert.Equal(1, snapshot.Buckets[0].Count);
    }

    [Fact]
    public void Concurrent_add_is_thread_safe()
    {
        var histogram = new LatencyHistogram();
        Parallel.For(0, 10_000, i => histogram.Add(i % 1000));

        Assert.Equal(10_000, histogram.Snapshot().TotalCount);
    }

    [Fact]
    public void Merge_preserves_bucket_layout_and_adds_counts()
    {
        var first = LatencyHistogram.FromSamples(new[] { 1d, 10d, 100d });
        var second = LatencyHistogram.FromSamples(new[] { 1d, 100d, 100_000d });

        var merged = LatencyHistogram.Merge(first, second);

        Assert.Equal(6, merged.TotalCount);
        Assert.Equal(2, merged.Buckets[1].Count);
        Assert.Equal(2, merged.Buckets[6].Count);
        Assert.Equal(2, merged.Buckets[15].Count);
    }

    [Fact]
    public void Merge_handles_null_aggregate_without_mutating_next()
    {
        var next = LatencyHistogram.FromSamples(new[] { 25d, 26d });

        var result = LatencyHistogram.Merge(null, next);

        Assert.Same(next, result);
    }

    [Fact]
    public void Snapshot_is_deterministic_and_serializable()
    {
        var first = LatencyHistogram.FromSamples(new[] { 10d, 50d, 1_000d });
        var second = LatencyHistogram.FromSamples(new[] { 10d, 50d, 1_000d });

        var a = JsonSerializer.Serialize(first);
        var b = JsonSerializer.Serialize(second);

        Assert.Equal(a, b);
        Assert.Equal(3, first.TotalCount);
    }
}
