using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class GitWorkspaceIntegrityGateTests
{
    [Fact]
    public async Task VerifiesExactCleanCommit()
    {
        var git = new FakeGit(new Dictionary<string, string>
        {
            ["rev-parse --show-toplevel"] = "/workspace",
            ["rev-parse HEAD"] = "0123456789abcdef0123456789abcdef01234567",
            ["status --porcelain=v1 --untracked-files=all"] = ""
        });

        var result = await new GitWorkspaceIntegrityGate(git)
            .VerifyAsync(Path.GetTempPath(), "0123456789abcdef0123456789abcdef01234567", CancellationToken.None);

        Assert.True(result.IsVerified);
        Assert.True(result.IsClean);
        Assert.Equal(3, git.Calls.Count);
    }

    [Fact]
    public async Task BlocksCommitMismatch()
    {
        var git = new FakeGit(new Dictionary<string, string>
        {
            ["rev-parse --show-toplevel"] = "/workspace",
            ["rev-parse HEAD"] = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ["status --porcelain=v1 --untracked-files=all"] = ""
        });

        var result = await new GitWorkspaceIntegrityGate(git)
            .VerifyAsync(Path.GetTempPath(), "0123456789abcdef0123456789abcdef01234567", CancellationToken.None);

        Assert.Equal(WorkspaceIntegrityStatus.Blocked, result.Status);
        Assert.Contains("immutable task commit", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BlocksDirtyOrUntrackedWorkspace()
    {
        var git = new FakeGit(new Dictionary<string, string>
        {
            ["rev-parse --show-toplevel"] = "/workspace",
            ["rev-parse HEAD"] = "0123456789abcdef0123456789abcdef01234567",
            ["status --porcelain=v1 --untracked-files=all"] = "?? new-file.txt"
        });

        var result = await new GitWorkspaceIntegrityGate(git)
            .VerifyAsync(Path.GetTempPath(), "0123456789abcdef0123456789abcdef01234567", CancellationToken.None);

        Assert.Equal(WorkspaceIntegrityStatus.Blocked, result.Status);
        Assert.False(result.IsClean);
        Assert.Contains("untracked", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectsInvalidCommitBeforeGitExecution()
    {
        var git = new FakeGit(new Dictionary<string, string>());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new GitWorkspaceIntegrityGate(git).VerifyAsync(Path.GetTempPath(), "not-a-sha", CancellationToken.None));
        Assert.Empty(git.Calls);
    }

    private sealed class FakeGit : IGitCommandExecutor
    {
        private readonly IReadOnlyDictionary<string, string> _responses;
        public List<string> Calls { get; } = new();

        public FakeGit(IReadOnlyDictionary<string, string> responses) => _responses = responses;

        public Task<string> ExecuteAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            var key = string.Join(" ", arguments);
            Calls.Add(key);
            return Task.FromResult(_responses.TryGetValue(key, out var response) ? response : "");
        }
    }
}
