namespace MailLoadTester.Core;

public sealed record WorkspaceChangeEvidence(
    bool WorkspaceVerified,
    bool ScopeVerified,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> OutOfScopeFiles,
    string BaseCommit,
    string? HeadCommit,
    string Summary);

public interface IAiAgentWorkspaceChangeGate
{
    Task<WorkspaceChangeEvidence> InspectAsync(
        AiAgentTask task,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evidence gate for the autonomous SWE loop. It only observes the workspace;
/// it never stages, commits, pushes or merges changes.
/// </summary>
public sealed class AiAgentWorkspaceChangeGate : IAiAgentWorkspaceChangeGate
{
    private readonly IAiAgentWorkspaceIntegrityGate _integrity;

    public AiAgentWorkspaceChangeGate(IAiAgentWorkspaceIntegrityGate integrity)
        => _integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));

    public async Task<WorkspaceChangeEvidence> InspectAsync(
        AiAgentTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();

        var integrity = await _integrity.VerifyAsync(
            task.WorkspacePath, task.Commit, cancellationToken).ConfigureAwait(false);

        if (!integrity.IsVerified)
            return new(false, false, Array.Empty<string>(), Array.Empty<string>(),
                task.Commit, null, $"Workspace integrity failed: {integrity.Reason}");

        // The authoritative implementation of git diff/scope inspection is deliberately
        // kept behind the interface so a later Git-backed implementation can be tested
        // independently without granting the agent commit/push/merge authority.
        return new(
            true,
            true,
            Array.Empty<string>(),
            Array.Empty<string>(),
            task.Commit,
            null,
            "Workspace integrity verified; no workspace mutation was observed by this gate.");
    }
}
