namespace MailLoadTester.Core;

public enum AgentExecutionStatus
{
    Completed,
    Failed,
    Cancelled,
    Blocked,
    TimedOut
}

public sealed record AgentExecutionRequest(
    string TaskId,
    string AgentRole,
    string Repository,
    string ImmutableCommit,
    string WorkspacePath,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AcceptanceCriteria,
    TimeSpan TimeBudget,
    int MaxIterations,
    bool RealTargetRequired = false);

public sealed record AgentExecutionResult(
    AgentExecutionStatus Status,
    string TaskId,
    string? Commit,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> Commands,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Diagnostics,
    bool TestsPassed,
    bool SecurityPassed,
    int Iterations,
    TimeSpan Duration);

public interface IAgentExecutionBackend
{
    string Name { get; }

    Task<AgentExecutionResult> ExecuteAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AgentExecutionPolicy(
    TimeSpan MaximumTaskDuration,
    int MaximumIterations,
    int MaximumChangedFiles,
    bool RequireIsolatedWorkspace,
    bool RequireEvidence,
    bool RequireSecurityGate,
    bool RequireTests,
    bool AllowNetworkAccess);

public sealed class AgentExecutionPolicyValidator
{
    public void Validate(AgentExecutionRequest request, AgentExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);

        if (string.IsNullOrWhiteSpace(request.TaskId))
            throw new ArgumentException("TaskId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Repository))
            throw new ArgumentException("Repository is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ImmutableCommit))
            throw new ArgumentException("ImmutableCommit is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.WorkspacePath))
            throw new ArgumentException("WorkspacePath is required.", nameof(request));
        if (request.TimeBudget <= TimeSpan.Zero || request.TimeBudget > policy.MaximumTaskDuration)
            throw new ArgumentOutOfRangeException(nameof(request.TimeBudget));
        if (request.MaxIterations is < 1 || request.MaxIterations > policy.MaximumIterations)
            throw new ArgumentOutOfRangeException(nameof(request.MaxIterations));
        if (request.AllowedScopes.Count == 0)
            throw new ArgumentException("At least one allowed scope is required.", nameof(request));
        if (request.AcceptanceCriteria.Count == 0)
            throw new ArgumentException("At least one acceptance criterion is required.", nameof(request));
        if (request.RealTargetRequired && !policy.AllowNetworkAccess)
            throw new InvalidOperationException("The execution policy denies network access for this task.");
        if (policy.RequireIsolatedWorkspace && !Directory.Exists(request.WorkspacePath))
            throw new DirectoryNotFoundException(request.WorkspacePath);
    }
}

public sealed class AutonomousAgentLoop
{
    private readonly IAgentExecutionBackend _backend;
    private readonly AgentExecutionPolicy _policy;
    private readonly AgentExecutionPolicyValidator _validator = new();

    public AutonomousAgentLoop(IAgentExecutionBackend backend, AgentExecutionPolicy policy)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async Task<AgentExecutionResult> ExecuteAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        _validator.Validate(request, _policy);
        cancellationToken.ThrowIfCancellationRequested();

        return await _backend.ExecuteAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }
}
