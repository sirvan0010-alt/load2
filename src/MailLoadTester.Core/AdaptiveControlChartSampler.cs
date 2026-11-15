namespace MailLoadTester;

/// <summary>One immutable point from the adaptive SEND-control history.</summary>
public sealed record AdaptiveControlChartSample(
    DateTimeOffset At,
    int? EffectiveIntervalMs,
    int? AdaptiveConcurrency,
    int ConfiguredConcurrency);

/// <summary>
/// UI-independent, bounded sampler for adaptive pacing telemetry. Null adaptive
/// concurrency means the feature is disabled or has no valid observation.
/// </summary>
public sealed class AdaptiveControlChartSampler
{
    public const int MaxSamples = 120;
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly List<AdaptiveControlChartSample> _samples = new();

    public IReadOnlyList<AdaptiveControlChartSample> Samples => _samples;

    public bool AddSample(
        DateTimeOffset at,
        int? effectiveIntervalMs,
        int? adaptiveConcurrency,
        int configuredConcurrency)
    {
        if (_samples.Count > 0 && at - _samples[^1].At < SampleInterval)
            return false;

        _samples.Add(new AdaptiveControlChartSample(
            at,
            effectiveIntervalMs is >= 0 ? effectiveIntervalMs : null,
            adaptiveConcurrency is >= 1 ? adaptiveConcurrency : null,
            Math.Max(1, configuredConcurrency)));

        if (_samples.Count > MaxSamples)
            _samples.RemoveRange(0, _samples.Count - MaxSamples);

        return true;
    }

    public void Clear() => _samples.Clear();
}
