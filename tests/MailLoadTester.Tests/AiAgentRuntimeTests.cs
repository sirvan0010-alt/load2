using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AiAgentRuntimeTests
{
    [Fact]
    public async Task RunnerBlocksUnauthorizedRealTarget()
    {
        var runner = new AiAgentRunner(new AllowAuthorization(), new AcceptVerifier(), new VerifiedWorkspace());
        var result = await runner.RunAsync(TaskFor(realTarget: true, authorized: false), new NoOpAgent());
        Assert.Equal(AgentRunStatus.Blocked, result.Status);
        Assert.Contains("not marked authorized", result.Handoff, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunnerRequiresIndependentVerification()
    {
        var runner = new AiAgentRunner(new AllowAuthorization(), new RejectVerifier(), new VerifiedWorkspace());
        var result = await runner.RunAsync(TaskFor(realTarget: false, authorized: false), new NoOpAgent());
        Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task ModelAgentPassesImmutableCommitToAdapter()
    {
        var adapter = new RecordingAdapter();
        var agent = new ModelBackedAiAgent(adapter, new NoOpTools());
        var task = TaskFor(false, false);
        var result = await agent.ExecuteAsync(new AiAgentContext(task, CancellationToken.None, DateTimeOffset.UtcNow.AddMinutes(1), 1), CancellationToken.None);
        Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
        Assert.Equal(task.Commit, adapter.Request!.Commit);
        Assert.Contains(task.Commit, adapter.Request.Prompt, StringComparison.Ordinal);
    }

    private static AiAgentTask TaskFor(bool realTarget, bool authorized) => new("TEST-AI-001", "TEST_AGENT", "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567", new[] { "src/MailLoadTester.Core" }, new[] { "tests pass" }, realTarget, authorized, TimeSpan.FromSeconds(5), 2, Path.GetTempPath());

    private sealed class NoOpAgent : IAiAgent
    {
        public Task<AiAgentExecutionResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken) => Task.FromResult(new AiAgentExecutionResult(
            AgentRunStatus.NeedsEvidence, new[] { new AiAgentFinding("claim", Array.Empty<string>(), AgentEvidenceLevel.SourceDocumented) }, Array.Empty<string>(),
            new Dictionary<string, string>(), Array.Empty<string>(), true, true, true, true, true, "verify"));
    }

    private sealed class AllowAuthorization : IAiAgentAuthorizationPolicy
    { public Task<bool> AuthorizeAsync(AiAgentTask task, CancellationToken cancellationToken) => Task.FromResult(true); }
    private sealed class AcceptVerifier : IAiAgentResultVerifier
    { public Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken) => Task.FromResult(true); }
    private sealed class RejectVerifier : IAiAgentResultVerifier
    { public Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken) => Task.FromResult(false); }
    private sealed class VerifiedWorkspace : IAiAgentWorkspaceIntegrityGate
    { public Task<WorkspaceIntegrityResult> VerifyAsync(string workspacePath, string expectedCommit, CancellationToken cancellationToken) => Task.FromResult(new WorkspaceIntegrityResult(WorkspaceIntegrityStatus.Verified, workspacePath, workspacePath, expectedCommit, expectedCommit, true, true, "verified")); }

    private sealed class RecordingAdapter : IAiAgentModelAdapter
    {
        public AiAgentModelRequest? Request { get; private set; }
        public Task<AiAgentModelResponse> GenerateAsync(AiAgentModelRequest request, CancellationToken cancellationToken)
        { Request = request; return Task.FromResult(new AiAgentModelResponse("model output", Array.Empty<string>())); }
    }
    private sealed class NoOpTools : IAiAgentToolExecutor
    { public Task<string> ExecuteAsync(string operation, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken) => Task.FromResult(string.Empty); }
}
