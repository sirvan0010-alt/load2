using System.Collections.ObjectModel;

namespace MailLoadTester.Core;

/// <summary>
/// Evidence levels inspired by the repository's hardware/diagnostic evidence ladder.
/// Higher levels must never be inferred from lower levels.
/// </summary>
public enum AgentEvidenceLevel
{
    SourceDocumented,
    StaticAnalysis,
    UnitTested,
    CiVerified,
    ControlledLoadVerified,
    ProductionObserved
}

public enum AgentRunStatus
{
    Ready,
    Blocked,
    NeedsEvidence
}

public sealed record AiAgentTask(
    string TaskId,
    string AgentRole,
    string Repository,
    string Commit,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AcceptanceCriteria,
    bool RealTargetRequired,
    bool Authorized,
    TimeSpan TimeBudget,
    int MaxIterations);

public sealed record AiAgentFinding(
    string Claim,
    IReadOnlyList<string> Evidence,
    AgentEvidenceLevel EvidenceLevel);

public sealed record AiAgentExecutionResult(
    AgentRunStatus Status,
    IReadOnlyList<AiAgentFinding> Findings,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyDictionary<string, string> Acceptance,
    IReadOnlyList<string> Tests,
    bool CiRequired,
    bool AuthorizationPassed,
    bool CancellationPassed,
    bool HardLimitsPassed,
    bool SecretsPassed,
    string Handoff);

public sealed record AiAgentContext(
    AiAgentTask Task,
    CancellationToken CancellationToken,
    DateTimeOffset Deadline,
    int Iteration);

/// <summary>Model/backend boundary. No provider is coupled to the core runner.</summary>
public interface IAiAgent
{
    Task<AiAgentExecutionResult> ExecuteAsync(
        AiAgentContext context,
        CancellationToken cancellationToken);
}

/// <summary>Explicit allow-listed tool boundary. Implementations decide which tools exist.</summary>
public interface IAiAgentToolExecutor
{
    bool IsAllowed(string toolName);

    Task<string> ExecuteAsync(
        string toolName,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken);
}

/// <summary>Authorization and safety policy. The runner never bypasses this policy.</summary>
public interface IAiAgentAuthorizationPolicy
{
    ValueTask<bool> ValidateAsync(AiAgentTask task, CancellationToken cancellationToken);
}

public interface IAiAgentResultVerifier
{
    ValueTask<bool> VerifyAsync(
        AiAgentTask task,
        AiAgentExecutionResult result,
        CancellationToken cancellationToken);
}

public sealed class AiAgentRunner
{
    private readonly IAiAgentAuthorizationPolicy _authorizationPolicy;
    private readonly IAiAgentResultVerifier _resultVerifier;

    public AiAgentRunner(
        IAiAgentAuthorizationPolicy authorizationPolicy,
        IAiAgentResultVerifier resultVerifier)
    {
        _authorizationPolicy = authorizationPolicy ?? throw new ArgumentNullException(nameof(authorizationPolicy));
        _resultVerifier = resultVerifier ?? throw new ArgumentNullException(nameof(resultVerifier));
    }

    public async Task<AiAgentExecutionResult> RunAsync(
        AiAgentTask task,
        IAiAgent agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(agent);

        ValidateTask(task);

        if (!await _authorizationPolicy.ValidateAsync(task, cancellationToken).ConfigureAwait(false))
        {
            return Blocked("Authorization policy rejected the task.");
        }

        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(task.TimeBudget);

        AiAgentExecutionResult? last = null;
        for (var iteration = 1; iteration <= task.MaxIterations; iteration++)
        {
            budgetCts.Token.ThrowIfCancellationRequested();

            var context = new AiAgentContext(
                task,
                budgetCts.Token,
                DateTimeOffset.UtcNow + budgetCts.RemainingTime(),
                iteration);

            last = await agent.ExecuteAsync(context, budgetCts.Token).ConfigureAwait(false);

            // The agent is never allowed to self-certify. A separate verifier decides whether
            // the produced result is acceptable for the current handoff.
            if (await _resultVerifier.VerifyAsync(task, last, budgetCts.Token).ConfigureAwait(false))
                return last;
        }

        return last is null
            ? Blocked("Agent produced no result.")
            : last with
            {
                Status = AgentRunStatus.NeedsEvidence,
                Handoff = "External verification did not accept the result within the configured iteration budget."
            };
    }

    private static void ValidateTask(AiAgentTask task)
    {
        if (string.IsNullOrWhiteSpace(task.TaskId))
            throw new ArgumentException("TaskId is required.", nameof(task));
        if (string.IsNullOrWhiteSpace(task.AgentRole))
            throw new ArgumentException("AgentRole is required.", nameof(task));
        if (!string.Equals(task.Repository, "sirvan0010-alt/load2", StringComparison.Ordinal))
            throw new ArgumentException("Task repository must be the load2 source of truth.", nameof(task));
        if (task.Commit.Length != 40 || task.Commit.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Task commit must be a 40-character SHA-1.", nameof(task));
        if (task.MaxIterations is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(task), "MaxIterations must be between 1 and 20.");
        if (task.TimeBudget is < TimeSpan.FromSeconds(1) or > TimeSpan.FromMinutes(30))
            throw new ArgumentOutOfRangeException(nameof(task), "TimeBudget must be between 1 second and 30 minutes.");
        if (task.RealTargetRequired && !task.Authorized)
            throw new InvalidOperationException("A real-target task requires explicit authorization.");
    }

    private static AiAgentExecutionResult Blocked(string reason) => new(
        AgentRunStatus.Blocked,
        Array.Empty<AiAgentFinding>(),
        Array.Empty<string>(),
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
        Array.Empty<string>(),
        CiRequired: true,
        AuthorizationPassed: false,
        CancellationPassed: true,
        HardLimitsPassed: true,
        SecretsPassed: true,
        Handoff: reason);
}

internal static class CancellationTokenSourceExtensions
{
    public static TimeSpan RemainingTime(this CancellationTokenSource source)
    {
        // CancellationTokenSource does not expose its deadline. The runner only uses this
        // value as informational context, so a conservative zero duration is preferable to
        // inventing an exact deadline.
        return TimeSpan.Zero;
    }
}
