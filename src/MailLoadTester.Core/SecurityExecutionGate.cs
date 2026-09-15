namespace MailLoadTester;

/// <summary>
/// B9 pre-execution security gate. It centralizes the ordered admission boundary
/// for typed scenarios while leaving pacing, concurrency, secrets and evidence to
/// the existing authoritative runtime components.
/// </summary>
public static class SecurityExecutionGate
{
    public static void Validate(LoadScenarioDefinition scenario, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        // SCOPE: the scenario must have an explicit, bounded identity.
        if (string.IsNullOrWhiteSpace(scenario.Name))
            throw new ArgumentException("Scenario scope requires a non-empty name.", nameof(scenario));

        cancellationToken.ThrowIfCancellationRequested();

        // AUTHORIZATION: live/non-test execution requires the existing explicit ack.
        if (!scenario.TestMode && !scenario.DryRun && !scenario.Unauthorized)
            throw new InvalidOperationException(
                "Security gate denied live scenario: explicit authorization is required.");

        cancellationToken.ThrowIfCancellationRequested();

        // HARD LIMIT: keep this gate aligned with the typed scenario contract.
        if (scenario.MessageCount is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(scenario.MessageCount), "MessageCount must be 1..10000.");
        if (scenario.MaxConcurrency is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(scenario.MaxConcurrency), "MaxConcurrency must be 1..20.");
        if (scenario.IntervalMs is < 0 or > 3_600_000)
            throw new ArgumentOutOfRangeException(nameof(scenario.IntervalMs));
        if (scenario.DurationSeconds is < 0 or > 86_400)
            throw new ArgumentOutOfRangeException(nameof(scenario.DurationSeconds));

        cancellationToken.ThrowIfCancellationRequested();

        // Runtime gates are authoritative and must not be duplicated here:
        // SmartPaceController -> pacing, worker/channel -> concurrency,
        // RunReport/ReplayableRunArtifact -> redaction/evidence.
    }
}
