namespace MailLoadTester;

/// <summary>Typed scenario families exposed by the load2 execution model.</summary>
public enum LoadScenarioKind
{
    NormalDelivery,
    BurstDelivery,
    SustainedLoad,
    ConnectionSaturation,
    ProviderDistribution,
    FailureInjection,
    MailboxQuota,
    Deliverability,
    SubscriptionBombSimulation,
    DoubleOptInSimulation,
    AntiAbuseControlSimulation,
    DistributedLab
}

/// <summary>
/// Explicit scenario tuning. Null values mean "keep the caller's existing setting".
/// No transport, queue, pacing or retry implementation lives here.
/// </summary>
public sealed record ScenarioTuning(
    int? MaxConcurrency = null,
    int? IntervalMs = null,
    bool? EnableBurstMode = null,
    int? BurstSize = null,
    int? BurstPauseSeconds = null,
    bool? PreWarmConnections = null,
    bool? EnableJitter = null,
    string? PaceProfile = null);

/// <summary>Immutable, validated description of one scenario.</summary>
public sealed record LoadScenarioDefinition(
    LoadScenarioKind Kind,
    SmtpScenario Scenario,
    ScenarioTuning? Tuning = null)
{
    public bool RequiresSimulation => Kind is
        LoadScenarioKind.FailureInjection or
        LoadScenarioKind.MailboxQuota or
        LoadScenarioKind.SubscriptionBombSimulation or
        LoadScenarioKind.DoubleOptInSimulation or
        LoadScenarioKind.AntiAbuseControlSimulation or
        LoadScenarioKind.DistributedLab;

    public bool RequiresMultipleAccounts => Kind == LoadScenarioKind.ProviderDistribution;

    public void Validate(MailTestOptions baseOptions)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);
        ArgumentNullException.ThrowIfNull(Scenario);
        Scenario.Limits.Validate();
        var tuning = Tuning ?? new ScenarioTuning();

        if (tuning.MaxConcurrency is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario MaxConcurrency musí být 1–20.");
        if (tuning.IntervalMs is < 0 or > 3_600_000)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario IntervalMs musí být 0–3600000 ms.");
        if (tuning.BurstSize is < 1 or > 1_000)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario BurstSize musí být 1–1000.");
        if (tuning.BurstPauseSeconds is < 0 or > 86_400)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario BurstPauseSeconds musí být 0–86400.");

        if (RequiresMultipleAccounts && (baseOptions.Accounts is null || baseOptions.Accounts.Count < 2))
            throw new ArgumentException(
                "ProviderDistribution vyžaduje alespoň dva SMTP účty/endpoints v MailTestOptions.Accounts.",
                nameof(baseOptions));
    }

    public MailTestOptions ApplyTo(MailTestOptions baseOptions)
    {
        Validate(baseOptions);
        var tuning = Tuning ?? new ScenarioTuning();
        var options = Scenario.ApplyTo(baseOptions);

        return Kind switch
        {
            LoadScenarioKind.NormalDelivery => ApplyTuning(options, tuning),
            LoadScenarioKind.BurstDelivery => ApplyTuning(options with
            {
                EnableBurstMode = tuning.EnableBurstMode ?? true,
                BurstSize = tuning.BurstSize ?? 10,
                BurstPauseSeconds = tuning.BurstPauseSeconds ?? 30
            }, tuning),
            LoadScenarioKind.SustainedLoad => ApplyTuning(options with
            {
                DurationSeconds = options.DurationSeconds > 0 ? options.DurationSeconds : 300
            }, tuning),
            LoadScenarioKind.ConnectionSaturation => ApplyTuning(options with
            {
                MaxConcurrency = tuning.MaxConcurrency ?? 20,
                PreWarmConnections = tuning.PreWarmConnections ?? true
            }, tuning),
            LoadScenarioKind.ProviderDistribution => ApplyTuning(options, tuning),
            LoadScenarioKind.Deliverability => ApplyTuning(options, tuning),
            _ => throw new NotSupportedException(
                $"Scénář {Kind} je definovaný, ale vyžaduje B4/B6 laboratorní adaptér a nelze jej spustit přes přímý SMTP runner.")
        };
    }

    private static MailTestOptions ApplyTuning(MailTestOptions options, ScenarioTuning tuning) => options with
    {
        MaxConcurrency = tuning.MaxConcurrency ?? options.MaxConcurrency,
        IntervalMs = tuning.IntervalMs ?? options.IntervalMs,
        EnableBurstMode = tuning.EnableBurstMode ?? options.EnableBurstMode,
        BurstSize = tuning.BurstSize ?? options.BurstSize,
        BurstPauseSeconds = tuning.BurstPauseSeconds ?? options.BurstPauseSeconds,
        PreWarmConnections = tuning.PreWarmConnections ?? options.PreWarmConnections,
        EnableJitter = tuning.EnableJitter ?? options.EnableJitter,
        PaceProfile = tuning.PaceProfile ?? options.PaceProfile
    };
}

/// <summary>Central catalog of bounded scenario definitions.</summary>
public static class LoadScenarioCatalog
{
    public static LoadScenarioDefinition Create(
        LoadScenarioKind kind,
        TargetSet targets,
        int messageCount,
        int durationSeconds = 0,
        ScenarioTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (messageCount is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(messageCount));
        if (durationSeconds is < 0 or > ScenarioLimits.MaxDurationSeconds)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        var limits = new ScenarioLimits(messageCount, durationSeconds);
        return new LoadScenarioDefinition(kind, new SmtpScenario(targets, limits), tuning);
    }
}

/// <summary>
/// Scenario adapter over the existing SMTP runner. It deliberately delegates all
/// execution to SmtpTestRunner: no second queue, pacing, concurrency or retry stack.
/// </summary>
public sealed class ScenarioEngine
{
    private readonly SmtpTestRunner _runner;

    public ScenarioEngine(SmtpTestRunner? runner = null)
    {
        _runner = runner ?? new SmtpTestRunner();
    }

    public MailTestOptions Prepare(LoadScenarioDefinition definition, MailTestOptions baseOptions)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.ApplyTo(baseOptions);
    }

    public Task<MailTestResult> RunAsync(
        LoadScenarioDefinition definition,
        MailTestOptions baseOptions,
        IProgress<ProgressUpdate> progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(progress);
        var options = Prepare(definition, baseOptions);
        return _runner.RunAsync(options, progress, ct);
    }
}
