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
    Task<AiAgentExecutionResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken);
}

public interface IAiAgentToolExecutor
{
    Task<string> ExecuteAsync(string operation, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken);
}

public interface IAiAgentAuthorizationPolicy
{
    Task<bool> AuthorizeAsync(AiAgentTask task, CancellationToken cancellationToken);
}

public interface IAiAgentResultVerifier
{
    Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken);
}

public interface IAiAgentWorkspaceIntegrityGate
{
    Task<WorkspaceIntegrityResult> VerifyAsync(string workspacePath, string expectedCommit, CancellationToken cancellationToken);
}

public sealed class AiAgentRunner
{
    private readonly IAiAgentAuthorizationPolicy _authorization;
    private readonly IAiAgentResultVerifier _verifier;
    private readonly IAiAgentWorkspaceIntegrityGate _workspaceIntegrity;
    private readonly IAiAgentChangeScopeGuard _changeScopeGuard;

    public AiAgentRunner(IAiAgentAuthorizationPolicy authorization, IAiAgentResultVerifier verifier, IAiAgentWorkspaceIntegrityGate workspaceIntegrity, IAiAgentChangeScopeGuard changeScopeGuard)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _workspaceIntegrity = workspaceIntegrity ?? throw new ArgumentNullException(nameof(workspaceIntegrity));
        _changeScopeGuard = changeScopeGuard ?? throw new ArgumentNullException(nameof(changeScopeGuard));
    }

    public async Task<AiAgentExecutionResult> RunAsync(AiAgentTask task, IAiAgent agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ValidateTask(task);
        cancellationToken.ThrowIfCancellationRequested();

        var workspace = await _workspaceIntegrity.VerifyAsync(task.WorkspacePath, task.Commit, cancellationToken).ConfigureAwait(false);
        if (!workspace.IsVerified)
            return Blocked($"Workspace integrity gate rejected execution: {workspace.Reason}");

        if (task.RealTargetRequired)
        {
            if (!task.Authorized)
                return Blocked("Real-target task is not marked authorized.");
            if (!await _authorization.AuthorizeAsync(task, cancellationToken).ConfigureAwait(false))
                return Blocked("Authorization policy rejected the task.");
        }

        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(task.TimeBudget);
        var deadline = DateTimeOffset.UtcNow.Add(task.TimeBudget);
        AiAgentExecutionResult? last = null;

        for (var iteration = 1; iteration <= task.MaxIterations; iteration++)
        {
            budgetCts.Token.ThrowIfCancellationRequested();
            last = await agent.ExecuteAsync(new AiAgentContext(task, budgetCts.Token, deadline, iteration), budgetCts.Token).ConfigureAwait(false);
            if (last.Status == AgentRunStatus.Blocked)
                return last;

            var scope = await _changeScopeGuard.VerifyAsync(task.WorkspacePath, task.AllowedScopes, budgetCts.Token).ConfigureAwait(false);
            if (!scope.IsAllowed)
                return last with { Status = AgentRunStatus.Blocked, ChangedFiles = scope.ChangedFiles, Handoff = $"Agent changed files outside allowed scopes: {string.Join(", ", scope.Violations)}" };

            if (await _verifier.VerifyAsync(task, last, budgetCts.Token).ConfigureAwait(false))
                return last with { Status = AgentRunStatus.Ready };
        }

        return last is null ? Blocked("Agent produced no result.") : last with
        {
            Status = AgentRunStatus.NeedsEvidence,
            Handoff = "Independent verification did not accept the result within the configured iteration budget."
        };
    }

    private static void ValidateTask(AiAgentTask task)
    {
        if (string.IsNullOrWhiteSpace(task.TaskId)) throw new ArgumentException("TaskId is required.", nameof(task));
        if (string.IsNullOrWhiteSpace(task.AgentRole)) throw new ArgumentException("AgentRole is required.", nameof(task));
        if (!string.Equals(task.Repository, "sirvan0010-alt/load2", StringComparison.Ordinal))
            throw new ArgumentException("Task repository must be the load2 source of truth.", nameof(task));
        if (task.Commit.Length != 40 || !task.Commit.All(Uri.IsHexDigit))
            throw new ArgumentException("Task commit must be a 40-character SHA-1.", nameof(task));
        if (string.IsNullOrWhiteSpace(task.WorkspacePath)) throw new ArgumentException("WorkspacePath is required.", nameof(task));
        ValidateScopes(task.AllowedScopes);
        if (task.AcceptanceCriteria is null || task.AcceptanceCriteria.Count == 0)
            throw new ArgumentException("At least one acceptance criterion is required.", nameof(task));
        if (task.MaxIterations is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(task), "MaxIterations must be between 1 and 20.");
        if (task.TimeBudget < TimeSpan.FromSeconds(1) || task.TimeBudget > TimeSpan.FromMinutes(30))
            throw new ArgumentOutOfRangeException(nameof(task), "TimeBudget must be between 1 second and 30 minutes.");
    }

    private static void ValidateScopes(IReadOnlyList<string> scopes)
    {
        if (scopes is null || scopes.Count == 0)
            throw new ArgumentException("At least one allowed scope is required.", nameof(scopes));

        foreach (var scope in scopes)
        {
            if (string.IsNullOrWhiteSpace(scope))
                throw new ArgumentException("Allowed scopes cannot contain empty values.", nameof(scopes));

            var normalized = scope.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(scope) ||
                normalized == ".." ||
                normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.Contains("/../", StringComparison.Ordinal) ||
                normalized.EndsWith("/..", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Allowed scope escapes the repository/workspace boundary: {scope}", nameof(scopes));
            }
        }
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
