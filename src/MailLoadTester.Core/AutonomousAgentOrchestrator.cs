namespace MailLoadTester.Core;

public sealed record AgentOrchestrationResult(
    IReadOnlyList<AgentExecutionResult> Results,
    bool Completed,
    string? Blocker);

/// <summary>
/// Coordinates specialist coding tasks through the single bounded execution loop.
/// It deliberately does not introduce another queue, pacing, retry or SMTP execution stack.
/// </summary>
public sealed class AutonomousAgentOrchestrator
{
    private readonly AutonomousAgentLoop _loop;

    public AutonomousAgentOrchestrator(AutonomousAgentLoop loop)
    {
        _loop = loop ?? throw new ArgumentNullException(nameof(loop));
    }

    public async Task<AgentOrchestrationResult> ExecuteAsync(
        IReadOnlyList<AgentExecutionRequest> tasks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0)
            return new AgentOrchestrationResult(Array.Empty<AgentExecutionResult>(), true, null);

        var taskIds = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<AgentExecutionResult>(tasks.Count);

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!taskIds.Add(task.TaskId))
                throw new ArgumentException($"Duplicate agent task id: {task.TaskId}", nameof(tasks));

            var result = await _loop.ExecuteAsync(task, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (result.Status is AgentExecutionStatus.Blocked or AgentExecutionStatus.Cancelled or AgentExecutionStatus.TimedOut)
            {
                return new AgentOrchestrationResult(results, false,
                    $"Specialist task '{task.TaskId}' stopped orchestration with status {result.Status}.");
            }

            if (result.Status != AgentExecutionStatus.Completed || !result.TestsPassed)
            {
                return new AgentOrchestrationResult(results, false,
                    $"Specialist task '{task.TaskId}' did not satisfy execution acceptance.");
            }
        }

        return new AgentOrchestrationResult(results, true, null);
    }
}
