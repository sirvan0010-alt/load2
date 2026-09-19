namespace MailLoadTester.Core;

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

public interface IAiAgentWorkspaceIntegrityGate
{
    Task<WorkspaceIntegrityResult> VerifyAsync(
        string workspacePath,
        string expectedCommit,
        CancellationToken cancellationToken);
}
