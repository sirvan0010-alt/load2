using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class AiAgentRunnerTests
{
    [Fact]
    public async Task BlocksWhenWorkspaceIntegrityFails()
    {
        var gate = new FakeWorkspace(false);
        var agent = new FakeAgent();
        var runner = CreateRunner(gate);

        var result = await runner.RunAsync(TaskFor(), agent);

        Assert.Equal(AgentRunStatus.Blocked, result.Status);
        Assert.Empty(agent.Calls);
    }

    [Fact]
    public async Task BlocksChangesOutsideAllowedScope()
    {
        var scope = new FakeScope(new AiAgentChangeScopeResult(
            false,
            new[] { "outside/file.cs" },
            new[] { "outside/file.cs" }));
        var agent = new FakeAgent();
        var runner = CreateRunner(new FakeWorkspace(true), scope);

        var result = await runner.RunAsync(TaskFor(), agent);

        Assert.Equal(AgentRunStatus.Blocked, result.Status);
        Assert.Contains("outside/file.cs", result.Handoff);
    }

    [Fact]
    public async Task StopsAfterVerification()
    {
        var agent = new FakeAgent();
        var verifier = new FakeVerifier(true);
        var runner = CreateRunner(new FakeWorkspace(true), new FakeScope(Allowed()), verifier);

        var result = await runner.RunAsync(TaskFor(), agent);

        Assert.Equal(AgentRunStatus.Ready, result.Status);
        Assert.Equal(1, agent.Calls);
    }

    private static AiAgentRunner CreateRunner(
        IAiAgentWorkspaceIntegrityGate workspace,
        IAiAgentChangeScopeGuard? scope = null,
        IAiAgentResultVerifier? verifier = null) =>
        new(
            new FakeAuthorization(),
            verifier ?? new FakeVerifier(false),
            workspace,
            scope ?? new FakeScope(Allowed()),
            new AiAgentAutonomyPolicy(2, TimeSpan.FromSeconds(5)));

    private static AiAgentChangeScopeResult Allowed() =>
        new(true, Array.Empty<string>(), Array.Empty<string>());

    private static AiAgentTask TaskFor() => new(
        "RUN-001",
        "ENGINEERING",
        "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/MailLoadTester.Core" },
        new[] { "verification passes" },
        false,
        false,
        TimeSpan.FromSeconds(5),
        2,
        Path.GetTempPath());

    private sealed class FakeWorkspace : IAiAgentWorkspaceIntegrityGate
    {
        private readonly bool _verified;
        public FakeWorkspace(bool verified) => _verified = verified;

        public Task<WorkspaceIntegrityResult> VerifyAsync(
            string workspacePath, string expectedCommit, CancellationToken cancellationToken) =>
            Task.FromResult(new WorkspaceIntegrityResult(
                _verified ? WorkspaceIntegrityStatus.Verified : WorkspaceIntegrityStatus.Blocked,
                workspacePath, workspacePath, expectedCommit, expectedCommit,
                true, _verified, _verified ? "ok" : "blocked"));
    }

    private sealed class FakeScope : IAiAgentChangeScopeGuard
    {
        private readonly AiAgentChangeScopeResult _result;
        public FakeScope(AiAgentChangeScopeResult result) => _result = result;

        public Task<AiAgentChangeScopeResult> VerifyAsync(
            string workspacePath, IReadOnlyList<string> allowedScopes, CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }

    private sealed class FakeAuthorization : IAiAgentAuthorizationPolicy
    {
        public Task<bool> AuthorizeAsync(AiAgentTask task, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FakeVerifier : IAiAgentResultVerifier
    {
        private readonly bool _accepted;
        public FakeVerifier(bool accepted) => _accepted = accepted;

        public Task<bool> VerifyAsync(
            AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken) =>
            Task.FromResult(_accepted);
    }

    private sealed class FakeAgent : IAiAgent
    {
        public int Calls { get; private set; }

        public Task<AiAgentExecutionResult> ExecuteAsync(
            AiAgentContext context, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new AiAgentExecutionResult(
                AgentRunStatus.NeedsEvidence,
                Array.Empty<AiAgentFinding>(),
                Array.Empty<string>(),
                new Dictionary<string, string>(),
                Array.Empty<string>(),
                true,
                !context.Task.RealTargetRequired,
                true,
                true,
                true,
                "test"));
        }
    }
}
