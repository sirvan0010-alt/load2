namespace MailLoadTester;

/// <summary>Typed post-baseline scenario families. Execution is delegated to the existing load2 engine.</summary>
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

/// <summary>Describes test intent without introducing a second execution pipeline.</summary>
public sealed record LoadScenarioDefinition(
    LoadScenarioKind Kind,
    string Name,
    int MessageCount,
    int MaxConcurrency,
    int IntervalMs,
    int DurationSeconds = 0,
    int Seed = 0,
    bool TestMode = true,
    bool DryRun = false,
    bool Unauthorized = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Scenario name is required.", nameof(Name));
        if (MessageCount is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(MessageCount), "MessageCount must be 1..10000.");
        if (MaxConcurrency is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrency), "MaxConcurrency must be 1..20.");
        if (IntervalMs is < 0 or > 3_600_000)
            throw new ArgumentOutOfRangeException(nameof(IntervalMs));
        if (DurationSeconds is < 0 or > 86_400)
            throw new ArgumentOutOfRangeException(nameof(DurationSeconds));
        if (!TestMode && !DryRun && !Unauthorized)
            throw new InvalidOperationException("Non-test scenario execution requires explicit authorization.");
    }
}
