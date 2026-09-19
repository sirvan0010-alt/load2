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
    Task<WorkspaceChangeEvidence> InspectAsync(AiAgentTask task, CancellationToken cancellationToken = default);
}

public sealed class AiAgentWorkspaceChangeGate : IAiAgentWorkspaceChangeGate
{
    private readonly IGitCommandExecutor _git;
    private readonly IAiAgentWorkspaceIntegrityGate _integrity;

    public AiAgentWorkspaceChangeGate(IAiAgentWorkspaceIntegrityGate integrity, IGitCommandExecutor? git = null)
    {
        _integrity = integrity ?? throw new ArgumentNullException(nameof(integrity));
        _git = git ?? new ProcessGitCommandExecutor();
    }

    public async Task<WorkspaceChangeEvidence> InspectAsync(AiAgentTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();

        var integrity = await _integrity.VerifyAsync(task.WorkspacePath, task.Commit, cancellationToken).ConfigureAwait(false);
        if (!integrity.IsVerified)
            return new(false, false, Array.Empty<string>(), Array.Empty<string>(), task.Commit, null,
                $"Workspace integrity failed: {integrity.Reason}");

        var diff = await _git.ExecuteAsync(integrity.RepositoryRoot,
            new[] { "diff", "--name-only", "--no-renames", task.Commit, "--" }, cancellationToken).ConfigureAwait(false);
        var untracked = await _git.ExecuteAsync(integrity.RepositoryRoot,
            new[] { "ls-files", "--others", "--exclude-standard" }, cancellationToken).ConfigureAwait(false);

        var changed = ParsePaths(diff).Concat(ParsePaths(untracked))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var outOfScope = changed.Where(path => !IsAllowed(path, task.AllowedScopes)).ToArray();
        var head = (await _git.ExecuteAsync(integrity.RepositoryRoot,
            new[] { "rev-parse", "HEAD" }, cancellationToken).ConfigureAwait(false)).Trim();

        return new(true, outOfScope.Length == 0, changed, outOfScope, task.Commit, head,
            outOfScope.Length == 0
                ? $"Detected {changed.Length} changed file(s); all are within the task scope."
                : $"Detected {outOfScope.Length} out-of-scope changed file(s).");
    }

    private static IEnumerable<string> ParsePaths(string output) =>
        output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
              .Where(x => !Path.IsPathRooted(x)).Select(x => x.Replace('\\', '/'));

    private static bool IsAllowed(string path, IReadOnlyList<string> scopes)
    {
        foreach (var scope in scopes)
        {
            var normalized = scope.Trim().Replace('\\', '/').Trim('/');
            if (normalized.Length == 0) return true;
            if (path.Equals(normalized, StringComparison.Ordinal) || path.StartsWith(normalized + "/", StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
