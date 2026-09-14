using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class GitWorkspaceIntegrityGateTests
{
    [Fact]
    public async Task VerifiesExactCommitAndCleanWorkspace()
    {
        using var workspace = TemporaryDirectory.Create();
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var git = new StubGitExecutor(
            workspace.Path,
            workspace.Path,
            commit,
            string.Empty);

        var result = await new GitWorkspaceIntegrityGate(git).VerifyAsync(workspace.Path, commit);

        Assert.True(result.IsVerified);
        Assert.Equal(commit, result.ActualCommit);
        Assert.True(result.IsGitRepository);
        Assert.True(result.IsClean);
    }

    [Fact]
    public async Task BlocksWhenHeadDoesNotMatchImmutableCommit()
    {
        using var workspace = TemporaryDirectory.Create();
        const string expected = "0123456789abcdef0123456789abcdef01234567";
        const string actual = "fedcba9876543210fedcba9876543210fedcba98";
        var git = new StubGitExecutor(workspace.Path, workspace.Path, actual, string.Empty);

        var result = await new GitWorkspaceIntegrityGate(git).VerifyAsync(workspace.Path, expected);

        Assert.Equal(WorkspaceIntegrityStatus.Blocked, result.Status);
        Assert.Contains("does not match", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlocksWhenWorkspaceIsDirty()
    {
        using var workspace = TemporaryDirectory.Create();
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var git = new StubGitExecutor(workspace.Path, workspace.Path, commit, " M src/file.cs\n?? untracked.txt\n");

        var result = await new GitWorkspaceIntegrityGate(git).VerifyAsync(workspace.Path, commit);

        Assert.Equal(WorkspaceIntegrityStatus.Blocked, result.Status);
        Assert.False(result.IsClean);
        Assert.Contains("not clean", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlocksMissingWorkspaceWithoutInvokingGit()
    {
        const string commit = "0123456789abcdef0123456789abcdef01234567";
        var git = new StubGitExecutor();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = await new GitWorkspaceIntegrityGate(git).VerifyAsync(missing, commit);

        Assert.Equal(WorkspaceIntegrityStatus.Blocked, result.Status);
        Assert.Equal(0, git.Calls);
    }

    [Fact]
    public async Task PropagatesCancellation()
    {
        using var workspace = TemporaryDirectory.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var git = new StubGitExecutor(workspace.Path, workspace.Path, "", string.Empty);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new GitWorkspaceIntegrityGate(git).VerifyAsync(
                workspace.Path,
                "0123456789abcdef0123456789abcdef01234567",
                cts.Token));
    }

    private sealed class StubGitExecutor : IGitCommandExecutor
    {
        private readonly string _root;
        private readonly string _head;
        private readonly string _status;

        public StubGitExecutor(string root = "", string? rootOutput = null, string head = "", string status = "")
        {
            _root = rootOutput ?? root;
            _head = head;
            _status = status;
        }

        public int Calls { get; private set; }

        public Task<string> ExecuteAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            var command = string.Join(' ', arguments);
            return Task.FromResult(command switch
            {
                "rev-parse --show-toplevel" => _root,
                "rev-parse HEAD" => _head,
                "status --porcelain=v1 --untracked-files=all" => _status,
                _ => throw new InvalidOperationException("Unexpected git command.")
            });
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        private TemporaryDirectory(string path) => Path = path;

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "load2-workspace-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
