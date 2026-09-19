using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class AiAgentChangeScopeGuardTests
{
    [Fact]
    public async Task AllowsChangesInsideScope()
    {
        var git = new FakeGit(
            " M src/MailLoadTester.Core/Changed.cs\n?? src/MailLoadTester.Core/New.cs\n");

        var result = await new GitChangeScopeGuard(git).VerifyAsync(
            "/workspace",
            new[] { "src/MailLoadTester.Core" },
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(
            new[]
            {
                "src/MailLoadTester.Core/Changed.cs",
                "src/MailLoadTester.Core/New.cs"
            },
            result.ChangedFiles);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task BlocksChangedFileOutsideScope()
    {
        var git = new FakeGit(
            " M src/MailLoadTester.Core/Changed.cs\n?? tests/Unexpected.cs\n");

        var result = await new GitChangeScopeGuard(git).VerifyAsync(
            "/workspace",
            new[] { "src/MailLoadTester.Core" },
            CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Contains("tests/Unexpected.cs", result.Violations);
    }

    [Fact]
    public async Task UsesNewPathForRenamedFile()
    {
        var git = new FakeGit(
            "R  src/MailLoadTester.Core/Old.cs -> src/MailLoadTester.Core/New.cs\n");

        var result = await new GitChangeScopeGuard(git).VerifyAsync(
            "/workspace",
            new[] { "src/MailLoadTester.Core" },
            CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Equal(
            new[] { "src/MailLoadTester.Core/New.cs" },
            result.ChangedFiles);
    }

    private sealed class FakeGit : IGitCommandExecutor
    {
        private readonly string _output;

        public FakeGit(string output) => _output = output;

        public Task<string> ExecuteAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken) =>
            Task.FromResult(_output);
    }
}
