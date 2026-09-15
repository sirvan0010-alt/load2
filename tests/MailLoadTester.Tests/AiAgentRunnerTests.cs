using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AiAgentRunnerTests
{
    [Fact]
    public async Task RejectsUnauthorizedRealTargetBeforeAgentExecution()
    {
        var policy = new RecordingAuthorizationPolicy(false);
        var verifier = new AcceptingVerifier();
        var agent = new RecordingAgent();
        var runner = CreateRunner(policy, verifier);

        var result = await runner.RunAsync(CreateTask(realTargetRequired: true, authorized: false), agent);

        Assert.Equal(AgentRunStatus.Blocked, result.Status);
        Assert.Equal(0, agent.Calls);
        // Soft-block on task.Authorized=false happens before the policy is consulted.
        Assert.Equal(0, policy.Calls);
        Assert.Contains("not marked authorized", result.Handoff, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectsWhenAuthorizationPolicyDeniesRealTarget()
    {
        var policy = new RecordingAuthorizationPolicy(false);
        var agent = new RecordingAgent();
        var runner = CreateRunner(policy, new AcceptingVerifier());

        var result = await runner.RunAsync(CreateTask(realTargetRequired: true, authorized: true), agent);

        Assert.Equal(AgentRunStatus.Blocked, result.Status);
        Assert.Equal(0, agent.Calls);
        Assert.Equal(1, policy.Calls);
    }

    [Fact]
    public async Task RejectsExecutionWhenWorkspaceIntegrityFails()
    {
        var agent = new RecordingAgent();
        var gate = new RecordingWorkspaceGate(false);
        var runner = new AiAgentRunner(
            new RecordingAuthorizationPolicy(true),
            new AcceptingVerifier(),
            gate);

        var result = await runner.RunAsync(CreateTask(), agent);

        Assert.Equal(AgentRunStatus.Blocked, result.Status);
        Assert.Equal(0, agent.Calls);
        Assert.Equal(1, gate.Calls);
    }

    [Fact]
    public async Task DoesNotAcceptAgentResultWithoutIndependentVerification()
    {
        var verifier = new RejectFirstThenAcceptVerifier();
        var agent = new RecordingAgent();
        var runner = CreateRunner(new RecordingAuthorizationPolicy(true), verifier);

        var result = await runner.RunAsync(CreateTask(maxIterations: 2), agent);

        Assert.Equal(AgentRunStatus.Ready, result.Status);
        Assert.Equal(2, agent.Calls);
        Assert.Equal(2, verifier.Calls);
    }

    [Fact]
    public async Task StopsAtIterationBudgetWhenVerifierNeverAccepts()
    {
        var verifier = new RejectingVerifier();
        var agent = new RecordingAgent();
        var runner = CreateRunner(new RecordingAuthorizationPolicy(true), verifier);

        var result = await runner.RunAsync(CreateTask(maxIterations: 3), agent);

        Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
        Assert.Equal(3, agent.Calls);
        Assert.Equal(3, verifier.Calls);
    }

    [Fact]
    public async Task PropagatesCancellationToWorkspaceGate()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var gate = new RecordingWorkspaceGate(true);
        var runner = new AiAgentRunner(
            new RecordingAuthorizationPolicy(true),
            new AcceptingVerifier(),
            gate);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync(CreateTask(), new RecordingAgent(), cts.Token));

        Assert.Equal(0, gate.Calls);
    }

    [Fact]
    public async Task PropagatesCancellationToAgent()
    {
        using var cts = new CancellationTokenSource();
        var policy = new RecordingAuthorizationPolicy(true);
        var verifier = new AcceptingVerifier();
        var agent = new CancellingAgent(cts);
        var runner = CreateRunner(policy, verifier);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync(CreateTask(), agent, cts.Token));
    }

    private static AiAgentRunner CreateRunner(
        IAiAgentAuthorizationPolicy policy,
        IAiAgentResultVerifier verifier)
        => new(policy, verifier, new RecordingWorkspaceGate(true));

    private static AiAgentTask CreateTask(
        bool realTargetRequired = false,
        bool authorized = true,
        int maxIterations = 1) => new(
            "TEST-AI-001",
            "TEST_AGENT",
            "sirvan0010-alt/load2",
            "0123456789abcdef0123456789abcdef01234567",
            new[] { "src/MailLoadTester.Core/**" },
            new[] { "runner verifies independently" },
            realTargetRequired,
            authorized,
            TimeSpan.FromSeconds(10),
            maxIterations,
            "/tmp/load2-agent-test");

    private sealed class RecordingWorkspaceGate(bool allowed) : IAiAgentWorkspaceIntegrityGate
    {
        public int Calls { get; private set; }

        public Task<WorkspaceIntegrityResult> VerifyAsync(
            string workspacePath,
            string expectedCommit,
            CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WorkspaceIntegrityResult(
                allowed ? WorkspaceIntegrityStatus.Verified : WorkspaceIntegrityStatus.Blocked,
                workspacePath,
                workspacePath,
                expectedCommit,
                allowed ? expectedCommit : string.Empty,
                allowed,
                allowed,
                allowed ? "verified" : "rejected"));
        }
    }

    private sealed class RecordingAuthorizationPolicy(bool allowed) : IAiAgentAuthorizationPolicy
    {
        public int Calls { get; private set; }

        public Task<bool> AuthorizeAsync(AiAgentTask task, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(allowed);
        }
    }

    private sealed class AcceptingVerifier : IAiAgentResultVerifier
    {
        public Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class RejectingVerifier : IAiAgentResultVerifier
    {
        public int Calls { get; private set; }

        public Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(false);
        }
    }

    private sealed class RejectFirstThenAcceptVerifier : IAiAgentResultVerifier
    {
        public int Calls { get; private set; }

        public Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Calls > 1);
        }
    }

    private sealed class RecordingAgent : IAiAgent
    {
        public int Calls { get; private set; }

        public Task<AiAgentExecutionResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(CreateResult());
        }
    }

    private sealed class CancellingAgent(CancellationTokenSource source) : IAiAgent
    {
        public Task<AiAgentExecutionResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken)
        {
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateResult());
        }
    }

    private static AiAgentExecutionResult CreateResult() => new(
        AgentRunStatus.Ready,
        Array.Empty<AiAgentFinding>(),
        Array.Empty<string>(),
        new Dictionary<string, string>(),
        Array.Empty<string>(),
        CiRequired: true,
        AuthorizationPassed: true,
        CancellationPassed: true,
        HardLimitsPassed: true,
        SecretsPassed: true,
        Handoff: "test");
}
