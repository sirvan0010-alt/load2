namespace MailLoadTester.Core;

public sealed record IsolatedWorkspaceResult(
    string WorkspacePath,
    string ExpectedCommit,
    WorkspaceIntegrityResult Integrity);

public interface IIsolatedGitWorkspaceFactory
{
    Task<IsolatedWorkspaceResult> CreateAsync(
        string sourceRepositoryPath,
        string workspacePath,
        string immutableCommit,
        CancellationToken cancellationToken);
}

/// <summary>
/// Creates a private local Git clone for agent execution. The source is local only;
/// no remote fetch/clone is performed. The resulting workspace is detached at the
/// requested immutable commit and must pass the independent integrity gate.
/// </summary>
public sealed class IsolatedGitWorkspaceFactory : IIsolatedGitWorkspaceFactory
{
    private readonly IGitCommandExecutor _git;
    private readonly IAiAgentWorkspaceIntegrityGate _integrityGate;

    public IsolatedGitWorkspaceFactory(
        IGitCommandExecutor? git = null,
        IAiAgentWorkspaceIntegrityGate? integrityGate = null)
    {
        _git = git ?? new ProcessGitCommandExecutor();
        _integrityGate = integrityGate ?? new GitWorkspaceIntegrityGate(_git);
    }

    public async Task<IsolatedWorkspaceResult> CreateAsync(
        string sourceRepositoryPath,
        string workspacePath,
        string immutableCommit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRepositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        if (immutableCommit.Length != 40 || !immutableCommit.All(Uri.IsHexDigit))
            throw new ArgumentException("Immutable commit must be a 40-character hexadecimal SHA-1.", nameof(immutableCommit));

        var source = Path.GetFullPath(sourceRepositoryPath);
        var destination = Path.GetFullPath(workspacePath);

        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"Source repository does not exist: {source}");
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Isolated workspace must differ from the source repository.", nameof(workspacePath));
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Isolated workspace path already exists.");

        var sourceIntegrity = await _integrityGate.VerifyAsync(
            source, immutableCommit, cancellationToken).ConfigureAwait(false);
        if (!sourceIntegrity.IsVerified)
            throw new InvalidOperationException($"Source repository failed integrity verification: {sourceIntegrity.Reason}");

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        try
        {
            await _git.ExecuteAsync(
                Path.GetDirectoryName(destination)!,
                new[] { "clone", "--no-local", "--no-hardlinks", "--no-checkout", "--", source, destination },
                cancellationToken).ConfigureAwait(false);

            await _git.ExecuteAsync(
                destination,
                new[] { "checkout", "--detach", "--", immutableCommit },
                cancellationToken).ConfigureAwait(false);

            var integrity = await _integrityGate.VerifyAsync(
                destination, immutableCommit, cancellationToken).ConfigureAwait(false);

            if (!integrity.IsVerified)
                throw new InvalidOperationException($"Created workspace failed integrity verification: {integrity.Reason}");

            return new IsolatedWorkspaceResult(destination, immutableCommit, integrity);
        }
        catch
        {
            TryDeleteDirectory(destination);
            throw;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Cleanup is best-effort; the original failure remains the failure signal.
        }
    }
}
