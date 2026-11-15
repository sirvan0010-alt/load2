namespace MailLoadTester;

/// <summary>
/// One immutable point in the SMTP/IMAP live-rate chart. Null values are gaps,
/// not zeroes; SMTP rates are per-minute estimates and IMAP is an observed
/// average between distinct timestamped mailbox snapshots.
/// </summary>
public sealed record RunMetricsChartSample(
    DateTimeOffset At,
    double? SendAttemptsPerMinute,
    double? AcceptedPerMinute,
    double? Smtp4xxPerMinute,
    double? Smtp5xxPerMinute,
    double? ObservedMailboxMessagesPerMinute,
    double? QuotaUsagePercent);

/// <summary>
/// UI-independent sampler for the live SMTP/IMAP chart. Keeping the calculation
/// here makes timing, counter resets, missing IMAP observations and history
/// bounds testable without constructing WinForms controls.
/// </summary>
public sealed class RunMetricsChartSampler
{
    public const int MaxSamples = 120;
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(10);

    private sealed record RawSample(
        DateTimeOffset At,
        int SendAttempts,
        int Accepted,
        int Smtp4xx,
        int Smtp5xx,
        double? ObservedMailboxMessagesPerMinute,
        double? QuotaUsagePercent);

    private readonly List<RawSample> _rawSamples = new();
    private readonly List<RunMetricsChartSample> _samples = new();
    private DateTimeOffset? _lastChartSampleAt;
    private DateTimeOffset? _lastQuotaObservationAt;
    private long? _lastQuotaMessageDelta;
    private double? _latestObservedMailboxMessagesPerMinute;
    private double? _latestQuotaUsagePercent;

    public IReadOnlyList<RunMetricsChartSample> Samples => _samples;

    /// <returns>True when a chart point was added; false when cadence throttling skipped it.</returns>
    public bool AddSample(
        DateTimeOffset at,
        int sendAttempts,
        int accepted,
        int smtp4xx,
        int smtp5xx,
        DateTimeOffset? quotaObservedAt,
        long? observedMailboxMessageDelta,
        double? quotaUsagePercent)
    {
        // IMAP observations arrive less often than progress events. Only a new
        // timestamp is a new observation; a failed observation deliberately
        // clears the series and breaks the next average.
        if (quotaObservedAt.HasValue
            && (!_lastQuotaObservationAt.HasValue || quotaObservedAt.Value > _lastQuotaObservationAt.Value))
        {
            if (_lastQuotaObservationAt.HasValue
                && _lastQuotaMessageDelta.HasValue
                && observedMailboxMessageDelta.HasValue)
            {
                var elapsed = (quotaObservedAt.Value - _lastQuotaObservationAt.Value).TotalMinutes;
                _latestObservedMailboxMessagesPerMinute = elapsed > 0
                    ? (observedMailboxMessageDelta.Value - _lastQuotaMessageDelta.Value) / elapsed
                    : null;
            }
            else
            {
                _latestObservedMailboxMessagesPerMinute = null;
            }

            _lastQuotaObservationAt = quotaObservedAt.Value;
            _lastQuotaMessageDelta = observedMailboxMessageDelta;
            _latestQuotaUsagePercent = quotaUsagePercent is >= 0 and <= 100 ? quotaUsagePercent : null;
        }

        if (_lastChartSampleAt.HasValue && at - _lastChartSampleAt.Value < SampleInterval)
            return false;

        _lastChartSampleAt = at;
        var raw = new RawSample(
            at,
            Math.Max(0, sendAttempts),
            Math.Max(0, accepted),
            Math.Max(0, smtp4xx),
            Math.Max(0, smtp5xx),
            _latestObservedMailboxMessagesPerMinute,
            _latestQuotaUsagePercent);

        _rawSamples.Add(raw);
        if (_rawSamples.Count > MaxSamples)
            _rawSamples.RemoveRange(0, _rawSamples.Count - MaxSamples);

        var cutoff = at - RateWindow;
        var baseline = _rawSamples.LastOrDefault(sample => sample.At <= cutoff)
            ?? _rawSamples[0];
        var elapsedMinutes = (at - baseline.At).TotalMinutes;

        double? NonNegativeRate(int current, int previous) =>
            elapsedMinutes > 0 && current >= previous
                ? (current - previous) / elapsedMinutes
                : null;

        _samples.Add(new RunMetricsChartSample(
            at,
            NonNegativeRate(raw.SendAttempts, baseline.SendAttempts),
            NonNegativeRate(raw.Accepted, baseline.Accepted),
            NonNegativeRate(raw.Smtp4xx, baseline.Smtp4xx),
            NonNegativeRate(raw.Smtp5xx, baseline.Smtp5xx),
            raw.ObservedMailboxMessagesPerMinute,
            raw.QuotaUsagePercent));

        if (_samples.Count > MaxSamples)
            _samples.RemoveRange(0, _samples.Count - MaxSamples);

        return true;
    }

    public void Clear()
    {
        _rawSamples.Clear();
        _samples.Clear();
        _lastChartSampleAt = null;
        _lastQuotaObservationAt = null;
        _lastQuotaMessageDelta = null;
        _latestObservedMailboxMessagesPerMinute = null;
        _latestQuotaUsagePercent = null;
    }
}
