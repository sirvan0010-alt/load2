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
    bool RealTargetRequired = false,
    string? RepairFeedback = null);

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

        using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCts.CancelAfter(request.TimeBudget);

        var started = DateTimeOffset.UtcNow;
        AgentExecutionResult? last = null;
        var feedback = request.RepairFeedback;

        for (var iteration = 1; iteration <= request.MaxIterations; iteration++)
        {
            budgetCts.Token.ThrowIfCancellationRequested();

            var attempt = request with { RepairFeedback = feedback };
            AgentExecutionResult result;
            try
            {
                result = await _backend.ExecuteAsync(attempt, budgetCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (budgetCts.IsCancellationRequested)
            {
                return BuildTerminal(last, request.TaskId, AgentExecutionStatus.Cancelled,
                    "Autonomous repair loop cancelled or exceeded its total time budget.",
                    iteration - 1, started);
            }

            last = result with
            {
                Iterations = iteration,
                Duration = DateTimeOffset.UtcNow - started
            };

            if (IsAccepted(last))
                return last;

            if (last.Status is AgentExecutionStatus.Blocked or AgentExecutionStatus.Cancelled or AgentExecutionStatus.TimedOut)
                return last;

            feedback = BuildRepairFeedback(last);
            if (string.IsNullOrWhiteSpace(feedback))
                break;
        }

        return last is null
            ? BuildTerminal(null, request.TaskId, AgentExecutionStatus.Failed,
                "Execution backend returned no result.", 0, started)
            : last with
            {
                Status = AgentExecutionStatus.Failed,
                Duration = DateTimeOffset.UtcNow - started,
                Diagnostics = last.Diagnostics
                    .Concat(new[] { "Bounded repair loop exhausted without satisfying the execution acceptance condition." })
                    .ToArray()
            };
    }

    private static bool IsAccepted(AgentExecutionResult result) =>
        result.Status == AgentExecutionStatus.Completed && result.TestsPassed;

    private static string? BuildRepairFeedback(AgentExecutionResult result)
    {
        var parts = result.Diagnostics
            .Concat(result.Evidence)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Take(12)
            .ToArray();

        return parts.Length == 0
            ? "Previous attempt did not satisfy the acceptance condition. Re-check the acceptance criteria, run the required tests, and correct the smallest safe defect."
            : "Previous attempt did not satisfy the acceptance condition. Observed evidence/diagnostics:\n- " +
              string.Join("\n- ", parts) +
              "\nRepair only within the allowed scopes and re-run the required tests.";
    }

    private static AgentExecutionResult BuildTerminal(
        AgentExecutionResult? last,
        string taskId,
        AgentExecutionStatus status,
        string diagnostic,
        int iterations,
        DateTimeOffset started)
    {
        return last is null
            ? new AgentExecutionResult(status, taskId, null, Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), new[] { diagnostic }, false, false, iterations,
                DateTimeOffset.UtcNow - started)
            : last with
            {
                Status = status,
                Iterations = iterations,
                Duration = DateTimeOffset.UtcNow - started,
                Diagnostics = last.Diagnostics.Concat(new[] { diagnostic }).ToArray()
            };
    }
}
