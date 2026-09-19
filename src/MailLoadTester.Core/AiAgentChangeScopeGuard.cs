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

        var output = await _git.ExecuteAsync(
            workspacePath,
            new[] { "diff", "--name-only", "--diff-filter=ACDMRTUXB", "HEAD" },
            cancellationToken);

        var files = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var scopes = allowedScopes
            .Where(static s => !string.IsNullOrWhiteSpace(s))
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var violations = files
            .Where(file => !scopes.Any(scope => IsWithinScope(file, scope)))
            .ToArray();

        return new AiAgentChangeScopeResult(violations.Length == 0, files, violations);
    }

    private static string Normalize(string value) =>
        value.Trim().Replace('\\', '/').Trim('/');

    private static bool IsWithinScope(string file, string scope) =>
        string.Equals(file, scope, StringComparison.Ordinal) ||
        file.StartsWith(scope + "/", StringComparison.Ordinal);
}
