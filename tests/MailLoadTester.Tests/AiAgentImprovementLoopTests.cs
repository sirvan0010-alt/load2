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
            Assert.Equal(AgentRunStatus.Completed, result.Status);
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
            var result = await loop.RunAsync(task, context, 2, TimeSpan.FromSeconds(5));
            Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
            Assert.Contains(result.Phases, p => p.Phase == AgentPhase.Test && !p.Succeeded);
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Fact]
    public async Task PolicyRejectsUnauthorizedRealTargetAndAutomaticMerge()
    {
        var workspace = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var task = CreateTask(workspace) with { RealTargetRequired = true, Authorized = false };
        Assert.Throws<InvalidOperationException>(() => new AiAgentAutonomyPolicy(2, TimeSpan.FromSeconds(5)).Validate(task));
        Assert.Throws<InvalidOperationException>(() => new AiAgentAutonomyPolicy(2, TimeSpan.FromSeconds(5), AllowAutomaticMerge: true).Validate(CreateTask(workspace)));
    }

    private static AiAgentTask CreateTask(string workspace) => new(
        "TEST-LOOP-001", "TEST_AGENT", "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/MailLoadTester.Core" }, new[] { "tests pass" },
        false, false, TimeSpan.FromSeconds(10), 2, workspace);

    private sealed class SuccessfulStep(AgentPhase phase) : IAiAgentImprovementStep
    {
        public AgentPhase Phase { get; } = phase;
        public Task<AiAgentStepResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new AiAgentStepResult(true, "ok", Array.Empty<string>()));
    }

    private sealed class FailedStep : IAiAgentImprovementStep
    {
        public AgentPhase Phase => AgentPhase.Test;
        public Task<AiAgentStepResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken) =>
            Task.FromResult(new AiAgentStepResult(false, "test failed", new[] { "failure" }));
    }

    private sealed class RecordingVerifier(bool accepted) : IAiAgentResultVerifier
    {
        public bool Called { get; private set; }
        public Task<bool> VerifyAsync(AiAgentTask task, AiAgentExecutionResult result, CancellationToken cancellationToken)
        { Called = true; return Task.FromResult(accepted); }
    }
}
