using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class IsolatedGitWorkspaceFactoryTests
{
    [Fact]
    public async Task CreatesDetachedWorkspaceAtExactCommitAndVerifiesIt()
    {
        var git = new RecordingGitExecutor();
        var gate = new StubIntegrityGate();
        var factory = new IsolatedGitWorkspaceFactory(git, gate);

        var root = Path.Combine(Path.GetTempPath(), "load2-tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "agent");
        Directory.CreateDirectory(source);

        try
        {
            var result = await factory.CreateAsync(
                source,
                destination,
                "0123456789abcdef0123456789abcdef01234567",
                CancellationToken.None);

            Assert.True(result.Integrity.IsVerified);
            Assert.Equal(destination, result.WorkspacePath);
            Assert.Contains(git.Commands, command => command.Contains("clone"));
            Assert.Contains(git.Commands, command => command.Contains("checkout"));
            Assert.True(gate.VerifiedPaths.Count >= 2);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsSourceEqualToDestination()
    {
        var root = Path.Combine(Path.GetTempPath(), "load2-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var factory = new IsolatedGitWorkspaceFactory(
                new RecordingGitExecutor(),
                new StubIntegrityGate());

            await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsync(
                root,
                root,
                "0123456789abcdef0123456789abcdef01234567",
                CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsExistingDestinationBeforeGitMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "load2-tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "agent");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);

        try
        {
            var git = new RecordingGitExecutor();
            var factory = new IsolatedGitWorkspaceFactory(git, new StubIntegrityGate());

            await Assert.ThrowsAsync<IOException>(() => factory.CreateAsync(
                source,
                destination,
                "0123456789abcdef0123456789abcdef01234567",
                CancellationToken.None));

            Assert.Empty(git.Commands);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationIsPropagatedBeforeClone()
    {
        var root = Path.Combine(Path.GetTempPath(), "load2-tests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var destination = Path.Combine(root, "agent");
        Directory.CreateDirectory(source);

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var git = new RecordingGitExecutor();
            var factory = new IsolatedGitWorkspaceFactory(git, new StubIntegrityGate());

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.CreateAsync(
                source,
                destination,
                "0123456789abcdef0123456789abcdef01234567",
                cts.Token));

            Assert.Empty(git.Commands);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class StubIntegrityGate : IAiAgentWorkspaceIntegrityGate
    {
        public List<string> VerifiedPaths { get; } = new();

        public Task<WorkspaceIntegrityResult> VerifyAsync(
            string workspacePath,
            string expectedCommit,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerifiedPaths.Add(Path.GetFullPath(workspacePath));
            return Task.FromResult(new WorkspaceIntegrityResult(
                WorkspaceIntegrityStatus.Verified,
                Path.GetFullPath(workspacePath),
                Path.GetFullPath(workspacePath),
                expectedCommit,
                expectedCommit,
                true,
                true,
                "test"));
        }
    }

    private sealed class RecordingGitExecutor : IGitCommandExecutor
    {
        public List<string> Commands { get; } = new();

        public Task<string> ExecuteAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add($"{workingDirectory}: {string.Join(' ', arguments)}");
            return Task.FromResult(arguments.Contains("rev-parse") ? "0123456789abcdef0123456789abcdef01234567\n" : string.Empty);
        }
    }
}
