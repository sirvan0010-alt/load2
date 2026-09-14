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
        var runner = new AiAgentRunner(policy, verifier);

        var result = await runner.RunAsync(CreateTask(realTargetRequired: true, authorized: false), agent);

        Assert.Equal(AgentRunStatus.Blocked, result.Status);
        Assert.Equal(0, agent.Calls);
        Assert.Equal(1, policy.Calls);
    }

    [Fact]
    public async Task DoesNotAcceptAgentResultWithoutIndependentVerification()
    {
        var verifier = new RejectFirstThenAcceptVerifier();
        var agent = new RecordingAgent();
        var runner = new AiAgentRunner(new RecordingAuthorizationPolicy(true), verifier);

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
        var runner = new AiAgentRunner(new RecordingAuthorizationPolicy(true), verifier);

        var result = await runner.RunAsync(CreateTask(maxIterations: 3), agent);

        Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
        Assert.Equal(3, agent.Calls);
        Assert.Equal(3, verifier.Calls);
    }

    [Fact]
    public async Task PropagatesCancellationToAgent()
    {
        using var cts = new CancellationTokenSource();
        var policy = new RecordingAuthorizationPolicy(true);
        var verifier = new AcceptingVerifier();
        var agent = new CancellingAgent(cts);
        var runner = new AiAgentRunner(policy, verifier);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            runner.RunAsync(CreateTask(), agent, cts.Token));
    }

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
            maxIterations);

    private sealed class RecordingAuthorizationPolicy(bool allowed) : IAiAgentAuthorizationPolicy
    {
        public int Calls { get; private set; }

        public ValueTask<bool> ValidateAsync(AiAgentTask task, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(allowed);
        }
    }

    private sealed class AcceptingVerifier : IAiAgentResultVerifier
    {
        public ValueTask<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
            => ValueTask.FromResult(true);
    }

    private sealed class RejectingVerifier : IAiAgentResultVerifier
    {
        public int Calls { get; private set; }

        public ValueTask<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(false);
        }
    }

    private sealed class RejectFirstThenAcceptVerifier : IAiAgentResultVerifier
    {
        public int Calls { get; private set; }

        public ValueTask<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(Calls > 1);
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
