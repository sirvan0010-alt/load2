using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class AiAgentImprovementLoopTests
{
    [Fact]
    public async Task CompletesOnlyAfterIndependentVerificationAndAllGates()
    {
        var verifier = new RecordingVerifier(true, true);
        var loop = new AiAgentImprovementLoop(new[] { new SuccessfulStep(AgentPhase.Research) }, verifier);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-loop-" + Guid.NewGuid())).FullName;
        try
        {
            var task = CreateTask(workspace);
            var context = new AiAgentContext(task, CancellationToken.None, DateTimeOffset.UtcNow.AddMinutes(1), 1);
            var result = await loop.RunAsync(task, context, 2, TimeSpan.FromSeconds(5));
            Assert.Equal(AgentRunStatus.Ready, result.Status);
            Assert.Equal("READY_FOR_HUMAN_MERGE_REVIEW", result.Handoff);
            Assert.True(verifier.Called);
            Assert.NotNull(verifier.Result);
            Assert.False(verifier.Result!.CancellationPassed);
            Assert.False(verifier.Result.HardLimitsPassed);
            Assert.False(verifier.Result.SecretsPassed);
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Fact]
    public async Task DoesNotCompleteWhenVerifierAcceptsButGatesAreMissing()
    {
        var verifier = new RecordingVerifier(true, false);
        var loop = new AiAgentImprovementLoop(new[] { new SuccessfulStep(AgentPhase.Research) }, verifier);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-loop-" + Guid.NewGuid())).FullName;
        try
        {
            var task = CreateTask(workspace);
            var context = new AiAgentContext(task, CancellationToken.None, DateTimeOffset.UtcNow.AddMinutes(1), 1);
            var result = await loop.RunAsync(task, context, 1, TimeSpan.FromSeconds(5));
            Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Fact]
    public async Task StopsOnFailedPhase()
    {
        var loop = new AiAgentImprovementLoop(new[] { new FailedStep() }, new RecordingVerifier(true, true));
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-loop-" + Guid.NewGuid())).FullName;
        try
        {
            var task = CreateTask(workspace);
            var context = new AiAgentContext(task, CancellationToken.None, DateTimeOffset.UtcNow.AddMinutes(1), 1);
            var result = await loop.RunAsync(task, context, 3, TimeSpan.FromSeconds(5));
            Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
            Assert.Contains("failed", result.Handoff, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(workspace, true); }
    }

    private static AiAgentTask CreateTask(string workspace) => new(
        "TEST-LOOP-001",
        "TEST_AGENT",
        "sirvan0010-alt/load2",
        "abc123",
        new[] { "src/MailLoadTester.Core" },
        new[] { "loop works" },
        RealTargetRequired: false,
        Authorized: false,
        TimeBudget: TimeSpan.FromSeconds(30),
        MaxIterations: 3,
        WorkspacePath: workspace);

    private sealed class SuccessfulStep : IAiAgentImprovementStep
    {
        public SuccessfulStep(AgentPhase phase) => Phase = phase;
        public AgentPhase Phase { get; }
        public Task<AiAgentStepResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new AiAgentStepResult(true, $"{Phase} ok", Array.Empty<string>()));
    }

    private sealed class FailedStep : IAiAgentImprovementStep
    {
        public AgentPhase Phase => AgentPhase.Implementation;
        public Task<AiAgentStepResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new AiAgentStepResult(false, "implementation failed", new[] { "bug" }));
    }

    private sealed class RecordingVerifier : IAiAgentResultVerifier
    {
        private readonly bool _accept;
        private readonly bool _allGates;
        public RecordingVerifier(bool accept, bool allGates) { _accept = accept; _allGates = allGates; }
        public bool Called { get; private set; }
        public AiAgentExecutionResult? Result { get; private set; }

        public Task<AiAgentVerificationResult> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
        {
            Called = true;
            Result = result;
            return Task.FromResult(new AiAgentVerificationResult(
                _accept,
                CiPassed: _allGates,
                AuthorizationPassed: _allGates,
                CancellationPassed: _allGates,
                HardLimitsPassed: _allGates,
                SecretsPassed: _allGates,
                Evidence: new[] { "test verifier evidence" },
                Handoff: "verified"));
        }
    }
}
