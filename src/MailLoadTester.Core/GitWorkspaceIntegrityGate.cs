using System.Diagnostics;

namespace MailLoadTester.Core;

public enum WorkspaceIntegrityStatus
{
    Verified,
    Blocked
}

public sealed record WorkspaceIntegrityResult(
    WorkspaceIntegrityStatus Status,
    string WorkspacePath,
    string RepositoryRoot,
    string ExpectedCommit,
    string ActualCommit,
    bool IsGitRepository,
    bool IsClean,
    string Reason)
{
    public bool IsVerified => Status == WorkspaceIntegrityStatus.Verified;
}

public interface IGitCommandExecutor
{
    Task<string> ExecuteAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

public sealed class GitWorkspaceIntegrityGate : IAiAgentWorkspaceIntegrityGate
{
    private readonly IGitCommandExecutor _git;

    public GitWorkspaceIntegrityGate(IGitCommandExecutor? git = null) =>
        _git = git ?? new ProcessGitCommandExecutor();

    public async Task<WorkspaceIntegrityResult> VerifyAsync(
        string workspacePath,
        string expectedCommit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        if (!Directory.Exists(workspacePath))
            return Blocked(workspacePath, expectedCommit, "Workspace directory does not exist.");

        if (!IsSha1(expectedCommit))
            throw new ArgumentException(
                "Expected commit must be a 40-character hexadecimal SHA-1.",
                nameof(expectedCommit));

        var fullPath = Path.GetFullPath(workspacePath);

        try
        {
            var root = (await _git.ExecuteAsync(
                fullPath,
                new[] { "rev-parse", "--show-toplevel" },
                cancellationToken).ConfigureAwait(false)).Trim();

            var actualCommit = (await _git.ExecuteAsync(
                fullPath,
                new[] { "rev-parse", "HEAD" },
                cancellationToken).ConfigureAwait(false)).Trim();

            var status = await _git.ExecuteAsync(
                fullPath,
                new[] { "status", "--porcelain=v1", "--untracked-files=all" },
                cancellationToken).ConfigureAwait(false);

            var clean = string.IsNullOrWhiteSpace(status);
            var commitMatches = string.Equals(
                actualCommit,
                expectedCommit,
                StringComparison.OrdinalIgnoreCase);

            if (!commitMatches)
            {
                return new(
                    WorkspaceIntegrityStatus.Blocked,
                    fullPath,
                    root,
                    expectedCommit,
                    actualCommit,
                    true,
                    clean,
                    "Workspace HEAD does not match the immutable task commit.");
            }

            if (!clean)
            {
                return new(
                    WorkspaceIntegrityStatus.Blocked,
                    fullPath,
                    root,
                    expectedCommit,
                    actualCommit,
                    true,
                    false,
                    "Workspace is not clean; uncommitted or untracked files are present.");
            }

            return new(
                WorkspaceIntegrityStatus.Verified,
                fullPath,
                root,
                expectedCommit,
                actualCommit,
                true,
                true,
                "Workspace is a clean Git worktree at the exact immutable commit.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return Blocked(
                fullPath,
                expectedCommit,
                $"Git workspace verification failed: {ex.Message}");
        }
    }

    private static WorkspaceIntegrityResult Blocked(
        string workspacePath,
        string expectedCommit,
        string reason) =>
        new(
            WorkspaceIntegrityStatus.Blocked,
            Path.GetFullPath(workspacePath),
            string.Empty,
            expectedCommit,
            string.Empty,
            false,
            false,
            reason);

    private static bool IsSha1(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length == 40 &&
        value.All(Uri.IsHexDigit);
}

internal sealed class ProcessGitCommandExecutor : IGitCommandExecutor
{
    public async Task<string> ExecuteAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
            throw new InvalidOperationException("Unable to start git process.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"git exited with code {process.ExitCode}."
                    : $"git exited with code {process.ExitCode}: {error.Trim()}");
        }

        return output;
    }
}
