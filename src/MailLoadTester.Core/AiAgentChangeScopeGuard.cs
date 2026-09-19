namespace MailLoadTester.Core;

public sealed record AiAgentChangeScopeResult(
    bool IsAllowed,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> Violations);

public interface IAiAgentChangeScopeGuard
{
    Task<AiAgentChangeScopeResult> VerifyAsync(
        string workspacePath,
        IReadOnlyList<string> allowedScopes,
        CancellationToken cancellationToken);
}

public sealed class GitChangeScopeGuard : IAiAgentChangeScopeGuard
{
    private readonly IGitCommandExecutor _git;

    public GitChangeScopeGuard(IGitCommandExecutor? git = null) =>
        _git = git ?? new ProcessGitCommandExecutor();

    public async Task<AiAgentChangeScopeResult> VerifyAsync(
        string workspacePath,
        IReadOnlyList<string> allowedScopes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(allowedScopes);

        var status = await _git.ExecuteAsync(
            workspacePath,
            new[] { "status", "--porcelain=v1", "--untracked-files=all" },
            cancellationToken).ConfigureAwait(false);

        var files = ParseChangedFiles(status);

        var scopes = allowedScopes
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = files
            .Where(file => !scopes.Any(scope => IsWithinScope(file, scope)))
            .ToArray();

        return new AiAgentChangeScopeResult(
            violations.Length == 0,
            files,
            violations);
    }

    private static IReadOnlyList<string> ParseChangedFiles(string status)
    {
        var files = new List<string>();

        foreach (var line in status.Split(
                     new[] { '', '
' },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3)
                continue;

            var path = line[2..].Trim();
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var renameSeparator = path.IndexOf(
                " -> ",
                StringComparison.Ordinal);

            if (renameSeparator >= 0)
                path = path[(renameSeparator + 4)..];

            files.Add(Normalize(path));
        }

        return files
            .Where(static file => file.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Normalize(string value) =>
        value.Trim().Replace('\', '/').Trim('/');

    private static bool IsWithinScope(string file, string scope) =>
        string.Equals(file, scope, StringComparison.Ordinal) ||
        file.StartsWith(scope + "/", StringComparison.Ordinal);
}
