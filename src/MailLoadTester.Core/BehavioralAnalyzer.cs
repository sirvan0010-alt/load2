namespace MailLoadTester;

public sealed record BehavioralMailEvent(
    DateTimeOffset Timestamp,
    string ProviderId,
    string SenderDomain,
    string Recipient,
    SimulatedMailOutcome Outcome,
    string? Workflow = null);

public sealed record BehavioralAnalysisOptions(
    TimeSpan BurstWindow,
    int BurstThreshold,
    double RecipientConcentrationThreshold,
    double AnomalyScoreThreshold)
{
    public static BehavioralAnalysisOptions Default { get; } = new(TimeSpan.FromSeconds(10), 20, 0.50, 0.60);

    public void Validate()
    {
        if (BurstWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(BurstWindow));
        if (BurstThreshold < 2) throw new ArgumentOutOfRangeException(nameof(BurstThreshold));
        if (RecipientConcentrationThreshold is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(RecipientConcentrationThreshold));
        if (AnomalyScoreThreshold is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(AnomalyScoreThreshold));
    }
}

public sealed record BehavioralAnalysisResult(
    int EventCount,
    double EventsPerSecond,
    int PeakEventsInBurst,
    double ProviderDiversity,
    double SenderDomainDiversity,
    double RecipientConcentration,
    double AnomalyScore,
    bool BurstDetected,
    bool ConcentrationDetected,
    bool Anomalous,
    IReadOnlyList<string> Findings);

/// <summary>Deterministic, side-effect-free analysis of supplied normalized mail events.</summary>
public sealed class BehavioralAnalyzer
{
    public BehavioralAnalysisResult Analyze(
        IEnumerable<BehavioralMailEvent> source,
        BehavioralAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= BehavioralAnalysisOptions.Default;
        options.Validate();

        var events = new List<BehavioralMailEvent>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(item);
            if (string.IsNullOrWhiteSpace(item.ProviderId)) throw new ArgumentException("ProviderId is required.", nameof(source));
            if (string.IsNullOrWhiteSpace(item.SenderDomain)) throw new ArgumentException("SenderDomain is required.", nameof(source));
            if (string.IsNullOrWhiteSpace(item.Recipient)) throw new ArgumentException("Recipient is required.", nameof(source));
            events.Add(item);
        }

        if (events.Count == 0)
            return new BehavioralAnalysisResult(0, 0, 0, 0, 0, 0, 0, false, false, false, Array.Empty<string>());

        events.Sort(static (a, b) => a.Timestamp.CompareTo(b.Timestamp));
        var elapsedSeconds = Math.Max((events[^1].Timestamp - events[0].Timestamp).TotalSeconds, 1.0);
        var rate = events.Count / elapsedSeconds;
        var peak = PeakInWindow(events, options.BurstWindow, cancellationToken);
        var providerDiversity = Diversity(events.Select(static e => e.ProviderId));
        var senderDiversity = Diversity(events.Select(static e => e.SenderDomain));
        var recipientConcentration = MaxConcentration(events.Select(static e => e.Recipient));
        var burstDetected = peak >= options.BurstThreshold;
        var concentrationDetected = recipientConcentration >= options.RecipientConcentrationThreshold;

        var burstScore = Math.Clamp((double)peak / options.BurstThreshold, 0, 1);
        var diversityScore = 1.0 - Math.Min(providerDiversity, senderDiversity);
        var anomalyScore = Math.Clamp((burstScore * 0.45) + (recipientConcentration * 0.35) + (diversityScore * 0.20), 0, 1);
        var findings = new List<string>();
        if (burstDetected) findings.Add("burst-velocity");
        if (concentrationDetected) findings.Add("recipient-concentration");
        if (providerDiversity < 0.50) findings.Add("low-provider-diversity");
        if (senderDiversity < 0.50) findings.Add("low-sender-domain-diversity");

        return new BehavioralAnalysisResult(events.Count, rate, peak, providerDiversity, senderDiversity,
            recipientConcentration, anomalyScore, burstDetected, concentrationDetected,
            anomalyScore >= options.AnomalyScoreThreshold, findings.AsReadOnly());
    }

    public BehavioralAnalysisResult Analyze(
        IEnumerable<SimulatedMailEvent> source,
        BehavioralAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Analyze(source.Select(static e => new BehavioralMailEvent(
            e.Timestamp, e.ProviderId, e.SenderDomain, e.Recipient, e.Outcome, e.Workflow)), options, cancellationToken);
    }

    private static int PeakInWindow(IReadOnlyList<BehavioralMailEvent> events, TimeSpan window, CancellationToken ct)
    {
        var peak = 0;
        var left = 0;
        for (var right = 0; right < events.Count; right++)
        {
            ct.ThrowIfCancellationRequested();
            while (events[right].Timestamp - events[left].Timestamp > window) left++;
            peak = Math.Max(peak, right - left + 1);
        }
        return peak;
    }

    private static double Diversity(IEnumerable<string> values)
    {
        var list = values.ToArray();
        return list.Length == 0 ? 0 : (double)list.Distinct(StringComparer.OrdinalIgnoreCase).Count() / list.Length;
    }

    private static double MaxConcentration(IEnumerable<string> values)
    {
        var list = values.ToArray();
        if (list.Length == 0) return 0;
        return (double)list.GroupBy(static x => x, StringComparer.OrdinalIgnoreCase).Max(static g => g.Count()) / list.Length;
    }
}
