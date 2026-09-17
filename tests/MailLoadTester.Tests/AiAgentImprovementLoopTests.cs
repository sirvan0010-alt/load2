using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AiAgentImprovementLoopTests
{
    [Fact]
    public async Task CompletesOnlyAfterIndependentVerification()
    {
        var verifier = new RecordingVerifier(true);
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
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Fact]
    public async Task StopsOnFailedPhase()
    {
        var loop = new AiAgentImprovementLoop(new[] { new FailedStep() }, new RecordingVerifier(true));
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
        public RecordingVerifier(bool accept) => _accept = accept;
        public bool Called { get; private set; }
        public Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(_accept);
        }
    }
}
