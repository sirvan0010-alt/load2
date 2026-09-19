using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class AiAgentChangeScopeGuardTests
{
    [Fact]
    public async Task AllowsTrackedAndUntrackedChangesInsideScope()
    {
        var git = new FakeGit(" M src/MailLoadTester.Core/Changed.cs\n?? src/MailLoadTester.Core/New.cs\n");
        var guard = new GitChangeScopeGuard(git);

        var result = await guard.VerifyAsync(
            "/workspace",
            new[] { "src/MailLoadTester.Core" },
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(
            new[] { "src/MailLoadTester.Core/Changed.cs", "src/MailLoadTester.Core/New.cs" },
            result.ChangedFiles);
        Assert.Empty(result.Violations);
        Assert.Equal(new[] { "status", "--porcelain=v1", "--untracked-files=all" }, git.Arguments);
    }

    [Fact]
    public async Task BlocksChangedFileOutsideScope()
    {
        var git = new FakeGit(" M src/MailLoadTester.Core/Changed.cs\n?? tests/Unexpected.cs\n");
        var guard = new GitChangeScopeGuard(git);

        var result = await guard.VerifyAsync(
            "/workspace",
            new[] { "src/MailLoadTester.Core" },
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Contains("tests/Unexpected.cs", result.Violations);
    }

    [Fact]
    public async Task UsesNewPathForRenamedFile()
    {
        var git = new FakeGit("R  src/MailLoadTester.Core/Old.cs -> src/MailLoadTester.Core/New.cs\n");
        var guard = new GitChangeScopeGuard(git);

        var result = await guard.VerifyAsync(
            "/workspace",
            new[] { "src/MailLoadTester.Core" },
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(new[] { "src/MailLoadTester.Core/New.cs" }, result.ChangedFiles);
    }

    private sealed class FakeGit : IGitCommandExecutor
    {
        private readonly string _output;

        public FakeGit(string output) => _output = output;

        public IReadOnlyList<string> Arguments { get; private set; } = Array.Empty<string>();

        public Task<string> ExecuteAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            Arguments = arguments.ToArray();
            return Task.FromResult(_output);
        }
    }
}
