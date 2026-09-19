using System.Collections.ObjectModel;

namespace MailLoadTester.Core;

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
    int MaxIterations,
    string WorkspacePath);

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

public interface IAiAgent
{
    Task<AiAgentExecutionResult> ExecuteAsync(
        AiAgentContext context,
        CancellationToken cancellationToken);
}

public interface IAiAgentToolExecutor
{
    Task<string> ExecuteAsync(
        string operation,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken cancellationToken);
}

public interface IAiAgentAuthorizationPolicy
{
    Task<bool> AuthorizeAsync(AiAgentTask task, CancellationToken cancellationToken);
}

public interface IAiAgentResultVerifier
{
    Task<bool> VerifyAsync(
        AiAgentTask task,
        AiAgentExecutionResult result,
        CancellationToken cancellationToken);
}

public interface IAiAgentWorkspaceIntegrityGate
{
    Task<WorkspaceIntegrityResult> VerifyAsync(
        string workspacePath,
        string expectedCommit,
        CancellationToken cancellationToken);
}

public sealed class AiAgentRunner
{
    private readonly IAiAgentAuthorizationPolicy _authorization;
    private readonly IAiAgentResultVerifier _verifier;
    private readonly IAiAgentWorkspaceIntegrityGate _workspaceIntegrity;

    public AiAgentRunner(
        IAiAgentAuthorizationPolicy authorization,
        IAiAgentResultVerifier verifier,
        IAiAgentWorkspaceIntegrityGate workspaceIntegrity)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _workspaceIntegrity = workspaceIntegrity ?? throw new ArgumentNullException(nameof(workspaceIntegrity));
    }

    public async Task<AiAgentExecutionResult> RunAsync(
        AiAgentTask task,
        IAiAgent agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ValidateTask(task);
        cancellationToken.ThrowIfCancellationRequested();

        var workspace = await _workspaceIntegrity.VerifyAsync(
            task.WorkspacePath,
            task.Commit,
            cancellationToken).ConfigureAwait(false);

        if (!workspace.IsVerified)
            return Blocked($"Workspace integrity gate rejected execution: {workspace.Reason}");

        if (task.RealTargetRequired && !await _authorization.AuthorizeAsync(task, cancellationToken).ConfigureAwait(false))
            return Blocked("Authorization policy rejected the task.");

        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(task.TimeBudget);
        var deadline = DateTimeOffset.UtcNow.Add(task.TimeBudget);
        AiAgentExecutionResult? last = null;

        for (var iteration = 1; iteration <= task.MaxIterations; iteration++)
        {
            budgetCts.Token.ThrowIfCancellationRequested();
            var context = new AiAgentContext(task, budgetCts.Token, deadline, iteration);
            last = await agent.ExecuteAsync(context, budgetCts.Token).ConfigureAwait(false);

            if (last.Status == AgentRunStatus.Blocked)
                return last;

            if (await _verifier.VerifyAsync(task, last, budgetCts.Token).ConfigureAwait(false))
                return last with { Status = AgentRunStatus.Ready };
        }

        return last is null
            ? Blocked("Agent produced no result.")
            : last with
            {
                Status = AgentRunStatus.NeedsEvidence,
                Handoff = "Independent verification did not accept the result within the configured iteration budget."
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
        if (task.Commit.Length != 40 || !task.Commit.All(Uri.IsHexDigit))
            throw new ArgumentException("Task commit must be a 40-character SHA-1.", nameof(task));
        if (string.IsNullOrWhiteSpace(task.WorkspacePath))
            throw new ArgumentException("WorkspacePath is required.", nameof(task));
        if (task.MaxIterations is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(task), "MaxIterations must be between 1 and 20.");
        if (task.TimeBudget < TimeSpan.FromSeconds(1) || task.TimeBudget > TimeSpan.FromMinutes(30))
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
