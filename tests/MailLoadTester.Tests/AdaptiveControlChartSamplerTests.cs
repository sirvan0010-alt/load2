using Xunit;

namespace MailLoadTester.Tests;

public class AdaptiveControlChartSamplerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 15, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Sampling_is_throttled_and_history_is_bounded()
    {
        var sampler = new AdaptiveControlChartSampler();

        Assert.True(sampler.AddSample(Start, 0, 4, 8));
        Assert.False(sampler.AddSample(Start.AddMilliseconds(500), 100, 3, 8));

        for (var second = 1; second <= 125; second++)
            Assert.True(sampler.AddSample(Start.AddSeconds(second), second * 100, 4, 8));

        Assert.Equal(AdaptiveControlChartSampler.MaxSamples, sampler.Samples.Count);
        Assert.Equal(Start.AddSeconds(6), sampler.Samples[0].At);
        Assert.Equal(Start.AddSeconds(125), sampler.Samples[^1].At);
    }

    [Fact]
    public void Disabled_adaptive_concurrency_is_a_gap_not_zero()
    {
        var sampler = new AdaptiveControlChartSampler();

        sampler.AddSample(Start, 250, null, 8);

        Assert.Equal(250, sampler.Samples[0].EffectiveIntervalMs);
        Assert.Null(sampler.Samples[0].AdaptiveConcurrency);
        Assert.Equal(8, sampler.Samples[0].ConfiguredConcurrency);
    }

    [Fact]
    public void Invalid_values_are_normalized_and_configured_workers_stay_positive()
    {
        var sampler = new AdaptiveControlChartSampler();

        sampler.AddSample(Start, -1, 0, 0);

        Assert.Null(sampler.Samples[0].EffectiveIntervalMs);
        Assert.Null(sampler.Samples[0].AdaptiveConcurrency);
        Assert.Equal(1, sampler.Samples[0].ConfiguredConcurrency);
    }

    [Fact]
    public void Clear_removes_the_entire_adaptive_history()
    {
        var sampler = new AdaptiveControlChartSampler();
        sampler.AddSample(Start, 100, 3, 8);

        sampler.Clear();

        Assert.Empty(sampler.Samples);
        Assert.True(sampler.AddSample(Start, 0, null, 8));
    }
}
