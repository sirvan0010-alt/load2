namespace MailLoadTester.Core;

public enum AgentPhase
{
    Research,
    Architecture,
    Implementation,
    Test,
    Security,
    Evidence,
    Verification,
    Completed,
    Blocked
}

public sealed record AgentPhaseRecord(
    AgentPhase Phase,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Succeeded,
    string Summary);

public sealed record AiAgentImprovementResult(
    AgentRunStatus Status,
    IReadOnlyList<AgentPhaseRecord> Phases,
    IReadOnlyList<string> Findings,
    int Iterations,
    string Handoff);

public interface IAiAgentImprovementStep
{
    AgentPhase Phase { get; }

    Task<AiAgentStepResult> ExecuteAsync(
        AiAgentContext context,
        CancellationToken cancellationToken);
}

public sealed record AiAgentStepResult(
    bool Succeeded,
    string Summary,
    IReadOnlyList<string> Findings);

/// <summary>
/// Bounded orchestration loop. It never changes authorization, network policy,
/// immutable commit or task scopes and never performs merge/release operations.
/// </summary>
public sealed class AiAgentImprovementLoop
{
    private readonly IReadOnlyList<IAiAgentImprovementStep> _steps;
    private readonly IAiAgentResultVerifier _verifier;

    public AiAgentImprovementLoop(
        IEnumerable<IAiAgentImprovementStep> steps,
        IAiAgentResultVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _steps = steps
            .GroupBy(step => step.Phase)
            .Select(group => group.Single())
            .OrderBy(step => step.Phase)
            .ToArray();

        if (_steps.Count == 0)
            throw new ArgumentException("At least one improvement step is required.", nameof(steps));
    }

    public async Task<AiAgentImprovementResult> RunAsync(
        AiAgentTask task,
        AiAgentContext context,
        int maxIterations,
        TimeSpan timeBudget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (maxIterations is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(maxIterations));
        if (timeBudget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeBudget));

        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(timeBudget);
        var token = budgetCts.Token;
        var phases = new List<AgentPhaseRecord>();
        var findings = new List<string>();

        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            token.ThrowIfCancellationRequested();
            var changed = false;

            foreach (var step in _steps)
            {
                token.ThrowIfCancellationRequested();
                var started = DateTimeOffset.UtcNow;
                var result = await step.ExecuteAsync(context, token).ConfigureAwait(false);
                var completed = DateTimeOffset.UtcNow;
                phases.Add(new AgentPhaseRecord(step.Phase, started, completed, result.Succeeded, result.Summary));
                findings.AddRange(result.Findings);

                if (!result.Succeeded)
                {
                    return new AiAgentImprovementResult(
                        AgentRunStatus.NeedsEvidence,
                        phases.AsReadOnly(),
                        findings.AsReadOnly(),
                        iteration,
                        $"Phase {step.Phase} failed; repair evidence is required before another iteration.");
                }

                changed |= result.Findings.Count > 0;
            }

            var execution = new AiAgentExecutionResult(
                AgentRunStatus.NeedsEvidence,
                findings.Select(f => new AiAgentFinding(f, Array.Empty<string>(), AgentEvidenceLevel.SourceDocumented)).ToArray(),
                Array.Empty<string>(),
                new Dictionary<string, string>(),
                Array.Empty<string>(),
                true, true, true, true, true,
                "Autonomous loop completed phase execution; independent verification required.");

            if (await _verifier.VerifyAsync(task, execution, token).ConfigureAwait(false))
            {
                phases.Add(new AgentPhaseRecord(AgentPhase.Verification, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, "Independent verifier accepted the execution result."));
                phases.Add(new AgentPhaseRecord(AgentPhase.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, "Bounded autonomous improvement completed."));
                return new AiAgentImprovementResult(AgentRunStatus.Ready, phases.AsReadOnly(), findings.AsReadOnly(), iteration, "READY_FOR_HUMAN_MERGE_REVIEW");
            }

            phases.Add(new AgentPhaseRecord(AgentPhase.Verification, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false, "Independent verifier rejected the execution result; repair iteration required."));
            if (!changed)
                break;
        }

        return new AiAgentImprovementResult(
            AgentRunStatus.NeedsEvidence,
            phases.AsReadOnly(),
            findings.AsReadOnly(),
            Math.Min(maxIterations, phases.Count),
            "VERIFICATION_NOT_SATISFIED; NO_AUTOMATIC_MERGE_OR_RELEASE");
    }
}
