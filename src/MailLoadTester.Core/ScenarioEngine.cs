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

/// <summary>
/// Immutable, validated description of one executable scenario.
/// </summary>
public sealed record LoadScenarioDefinition(
    LoadScenarioKind Kind,
    SmtpScenario Scenario,
    ScenarioTuning Tuning = null!)
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
        Scenario.Limits.Validate();

        if (Tuning.MaxConcurrency is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario MaxConcurrency musí být 1–20.");
        if (Tuning.IntervalMs is < 0 or > 3_600_000)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario IntervalMs musí být 0–3600000 ms.");
        if (Tuning.BurstSize is < 1 or > 1_000)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario BurstSize musí být 1–1000.");
        if (Tuning.BurstPauseSeconds is < 0 or > 86_400)
            throw new ArgumentOutOfRangeException(nameof(Tuning), "Scenario BurstPauseSeconds musí být 0–86400.");

        if (RequiresMultipleAccounts && (baseOptions.Accounts is null || baseOptions.Accounts.Count < 2))
            throw new ArgumentException(
                "ProviderDistribution vyžaduje alespoň dva SMTP účty/endpoints v MailTestOptions.Accounts.",
                nameof(baseOptions));

        // Simulation-only scenarios deliberately cannot silently fall through to the
        // real SMTP transport. Their execution is enabled later by the provider/mailbox lab.
        if (RequiresSimulation)
            return;
    }

    public MailTestOptions ApplyTo(MailTestOptions baseOptions)
    {
        Validate(baseOptions);
        var options = Scenario.ApplyTo(baseOptions);

        return Kind switch
        {
            LoadScenarioKind.NormalDelivery => ApplyTuning(options),
            LoadScenarioKind.BurstDelivery => ApplyTuning(options with
            {
                EnableBurstMode = Tuning.EnableBurstMode ?? true,
                BurstSize = Tuning.BurstSize ?? 10,
                BurstPauseSeconds = Tuning.BurstPauseSeconds ?? 30
            }),
            LoadScenarioKind.SustainedLoad => ApplyTuning(options with
            {
                DurationSeconds = options.DurationSeconds > 0 ? options.DurationSeconds : 300
            }),
            LoadScenarioKind.ConnectionSaturation => ApplyTuning(options with
            {
                MaxConcurrency = Tuning.MaxConcurrency ?? 20,
                PreWarmConnections = Tuning.PreWarmConnections ?? true
            }),
            LoadScenarioKind.ProviderDistribution => ApplyTuning(options),
            LoadScenarioKind.Deliverability => ApplyTuning(options),
            _ => throw new NotSupportedException(
                $"Scénář {Kind} je definovaný, ale zatím vyžaduje B4/B6 laboratorní adaptér a nelze jej spustit přes přímý SMTP runner.")
        };
    }

    private MailTestOptions ApplyTuning(MailTestOptions options)
    {
        return options with
        {
            MaxConcurrency = Tuning.MaxConcurrency ?? options.MaxConcurrency,
            IntervalMs = Tuning.IntervalMs ?? options.IntervalMs,
            EnableBurstMode = Tuning.EnableBurstMode ?? options.EnableBurstMode,
            BurstSize = Tuning.BurstSize ?? options.BurstSize,
            BurstPauseSeconds = Tuning.BurstPauseSeconds ?? options.BurstPauseSeconds,
            PreWarmConnections = Tuning.PreWarmConnections ?? options.PreWarmConnections,
            EnableJitter = Tuning.EnableJitter ?? options.EnableJitter,
            PaceProfile = Tuning.PaceProfile ?? options.PaceProfile
        };
    }
}

/// <summary>Central catalog of safe, bounded scenario defaults.</summary>
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
        return new LoadScenarioDefinition(kind, new SmtpScenario(targets, limits), tuning ?? new ScenarioTuning());
    }
}

/// <summary>
/// Scenario adapter over the existing SMTP runner. It intentionally delegates all
/// execution to SmtpTestRunner, so there is no second queue, pacing or retry stack.
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
