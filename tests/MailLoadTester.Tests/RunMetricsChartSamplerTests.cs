using Xunit;

namespace MailLoadTester.Tests;

public class RunMetricsChartSamplerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    private static bool Add(
        RunMetricsChartSampler sampler,
        int second,
        int sends = 0,
        int accepted = 0,
        int smtp4xx = 0,
        int smtp5xx = 0,
        int? quotaSecond = null,
        long? quotaMessageDelta = null,
        double? quotaPercent = null)
        => sampler.AddSample(
            Start.AddSeconds(second),
            sends,
            accepted,
            smtp4xx,
            smtp5xx,
            quotaSecond.HasValue ? Start.AddSeconds(quotaSecond.Value) : null,
            quotaMessageDelta,
            quotaPercent);

    [Fact]
    public void Sampling_is_throttled_to_one_point_per_second_and_history_is_bounded()
    {
        var sampler = new RunMetricsChartSampler();

        Assert.True(Add(sampler, 0));
        Assert.False(Add(sampler, 0));
        Assert.False(Add(sampler, 0) || Add(sampler, 0));
        Assert.False(sampler.AddSample(Start.AddMilliseconds(500), 1, 1, 0, 0, null, null, null));
        Assert.True(Add(sampler, 1));

        for (var second = 2; second <= 125; second++)
            Assert.True(Add(sampler, second, sends: second));

        Assert.Equal(RunMetricsChartSampler.MaxSamples, sampler.Samples.Count);
        Assert.Equal(Start.AddSeconds(6), sampler.Samples[0].At);
        Assert.Equal(Start.AddSeconds(125), sampler.Samples[^1].At);
    }

    [Fact]
    public void Smtp_rates_are_per_minute_and_counter_reset_creates_gap()
    {
        var sampler = new RunMetricsChartSampler();
        Add(sampler, 0, sends: 0, accepted: 0, smtp4xx: 0, smtp5xx: 0);
        Add(sampler, 10, sends: 100, accepted: 80, smtp4xx: 10, smtp5xx: 5);

        var point = sampler.Samples[^1];
        Assert.Equal(600, point.SendAttemptsPerMinute);
        Assert.Equal(480, point.AcceptedPerMinute);
        Assert.Equal(60, point.Smtp4xxPerMinute);
        Assert.Equal(30, point.Smtp5xxPerMinute);

        Add(sampler, 21, sends: 5, accepted: 4, smtp4xx: 1, smtp5xx: 0);
        Assert.Null(sampler.Samples[^1].SendAttemptsPerMinute);
        Assert.Null(sampler.Samples[^1].AcceptedPerMinute);

        Add(sampler, 32, sends: 15, accepted: 12, smtp4xx: 2, smtp5xx: 1);
        Assert.True(sampler.Samples[^1].SendAttemptsPerMinute >= 0);
    }

    [Fact]
    public void Repeated_progress_callbacks_do_not_recalculate_same_imap_snapshot()
    {
        var sampler = new RunMetricsChartSampler();
        Add(sampler, 0, quotaSecond: 0, quotaMessageDelta: 10, quotaPercent: 20);
        Add(sampler, 1, quotaSecond: 0, quotaMessageDelta: 10, quotaPercent: 20);

        Assert.Null(sampler.Samples[^1].ObservedMailboxMessagesPerMinute);
        Assert.Equal(20, sampler.Samples[^1].QuotaUsagePercent);

        Add(sampler, 30, quotaSecond: 30, quotaMessageDelta: 20, quotaPercent: 25);
        Assert.Equal(20d, sampler.Samples[^1].ObservedMailboxMessagesPerMinute!.Value, 6);
        Assert.Equal(25, sampler.Samples[^1].QuotaUsagePercent);
    }

    [Fact]
    public void Imap_failure_breaks_the_rate_until_two_new_valid_snapshots_exist()
    {
        var sampler = new RunMetricsChartSampler();
        Add(sampler, 0, quotaSecond: 0, quotaMessageDelta: 10, quotaPercent: 20);
        Add(sampler, 30, quotaSecond: 30, quotaMessageDelta: 20, quotaPercent: 30);
        Assert.Equal(20d, sampler.Samples[^1].ObservedMailboxMessagesPerMinute!.Value, 6);

        Add(sampler, 60, quotaSecond: 60, quotaMessageDelta: null, quotaPercent: null);
        Assert.Null(sampler.Samples[^1].ObservedMailboxMessagesPerMinute);
        Assert.Null(sampler.Samples[^1].QuotaUsagePercent);

        Add(sampler, 90, quotaSecond: 90, quotaMessageDelta: 25, quotaPercent: 35);
        Assert.Null(sampler.Samples[^1].ObservedMailboxMessagesPerMinute);

        Add(sampler, 120, quotaSecond: 120, quotaMessageDelta: 35, quotaPercent: 40);
        Assert.Equal(20d, sampler.Samples[^1].ObservedMailboxMessagesPerMinute!.Value, 6);
    }

    [Fact]
    public void Imap_message_decrease_is_preserved_as_a_negative_observation()
    {
        var sampler = new RunMetricsChartSampler();
        Add(sampler, 0, quotaSecond: 0, quotaMessageDelta: 40, quotaPercent: 60);
        Add(sampler, 30, quotaSecond: 30, quotaMessageDelta: 30, quotaPercent: 55);

        Assert.Equal(-20d, sampler.Samples[^1].ObservedMailboxMessagesPerMinute!.Value, 6);
    }

    [Fact]
    public void Clear_resets_history_cadence_and_quota_baseline()
    {
        var sampler = new RunMetricsChartSampler();
        Add(sampler, 0, quotaSecond: 0, quotaMessageDelta: 5, quotaPercent: 10);
        Add(sampler, 30, quotaSecond: 30, quotaMessageDelta: 10, quotaPercent: 20);

        sampler.Clear();

        Assert.Empty(sampler.Samples);
        Assert.True(Add(sampler, 31, quotaSecond: 31, quotaMessageDelta: 50, quotaPercent: 80));
        Assert.Null(sampler.Samples[0].ObservedMailboxMessagesPerMinute);
        Assert.Equal(80, sampler.Samples[0].QuotaUsagePercent);
    }

    [Fact]
    public void Invalid_quota_percent_is_not_plotted()
    {
        var sampler = new RunMetricsChartSampler();
        Add(sampler, 0, quotaSecond: 0, quotaMessageDelta: 1, quotaPercent: 140);

        Assert.Null(sampler.Samples[0].QuotaUsagePercent);
    }
}
