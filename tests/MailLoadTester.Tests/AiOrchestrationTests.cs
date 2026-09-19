using Xunit;
using MailLoadTester;

namespace MailLoadTester.Tests;

public sealed class AiOrchestrationTests
{
    private static MailTestOptions Options(
        bool testMode = true,
        bool unauthorized = false,
        int messageCount = 10,
        int maxConcurrency = 2)
        => new(
            From: "tester@example.test",
            Recipients: new[] { "recipient@example.test" },
            SmtpHost: "smtp.example.test",
            Port: 587,
            Security: SmtpSecurity.StartTls,
            UseAuthentication: false,
            Username: "",
            Password: "",
            MessageCount: messageCount,
            IntervalMs: 100,
            BatchMode: false,
            BatchSize: 1,
            BatchPauseSeconds: 1,
            MaxConcurrency: maxConcurrency,
            Subject: "test",
            Body: "test",
            DisplayName: "test",
            RandomTestData: false,
            TestMode: testMode,
            AllowedDomains: "example.test",
            HtmlBody: false,
            Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(),
            IgnoreCertificateErrors: false,
            MaxRetries: 0,
            DryRun: false,
            Unauthorized: unauthorized);

    [Fact]
    public async Task Guard_Allows_Bounded_TestMode_Action()
    {
        var options = Options();
        var context = new AiTaskContext(options, new HashSet<string> { "example.test" });
        var action = new AiAction("a1", AiActionKind.LoadTest, "SMTP_PROTOCOL_AGENT",
            new[] { "example.test" }, 5, 1, 0);

        var decision = await new AiActionGuard().ValidateAsync(action, context, CancellationToken.None);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public async Task Guard_Blocks_Unacknowledged_Real_Load()
    {
        var options = Options(testMode: false, unauthorized: false);
        var context = new AiTaskContext(options);
        var action = new AiAction("a1", AiActionKind.LoadTest, "LOAD_ENGINE_AGENT",
            new[] { "smtp.example.test" }, 1, 1, 0);

        var decision = await new AiActionGuard().ValidateAsync(action, context, CancellationToken.None);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task Guard_Rejects_Target_Outside_Scope()
    {
        var options = Options();
        var context = new AiTaskContext(options, new HashSet<string> { "example.test" });
        var action = new AiAction("a1", AiActionKind.Diagnostics, "NETWORK_AGENT",
            new[] { "other.example" }, 1, 1, 0);

        var decision = await new AiActionGuard().ValidateAsync(action, context, CancellationToken.None);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task Supervisor_Requires_Registered_Agent()
    {
        var options = Options();
        var context = new AiTaskContext(options);
        var registry = new MailLoadAgentRegistry(new[] { new StubAgent("SMTP_PROTOCOL_AGENT") });
        var supervisor = new AiSupervisor(registry, new AiActionGuard());
        var plan = new ExecutionPlan(new[]
        {
            new AiAction("a1", AiActionKind.Diagnostics, "UNKNOWN", new[] { "example.test" }, 1, 1, 0)
        });

        await Assert.ThrowsAsync<KeyNotFoundException>(
            async () => await supervisor.AuthorizePlanAsync(plan, context, CancellationToken.None));
    }

    [Fact]
    public async Task Supervisor_Propagates_Cancellation()
    {
        var options = Options();
        var context = new AiTaskContext(options);
        var registry = new MailLoadAgentRegistry(new[] { new StubAgent("SMTP_PROTOCOL_AGENT") });
        var supervisor = new AiSupervisor(registry, new AiActionGuard());
        var plan = new ExecutionPlan(new[]
        {
            new AiAction("a1", AiActionKind.Diagnostics, "SMTP_PROTOCOL_AGENT", new[] { "example.test" }, 1, 1, 0)
        });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await supervisor.AuthorizePlanAsync(plan, context, cts.Token));
    }


    [Fact]
    public async Task ConfiguredPlanner_Stays_Within_Configured_Bounds()
    {
        var options = Options(messageCount: 7, maxConcurrency: 2);
        var context = new AiTaskContext(options);
        var plan = await new ConfiguredExecutionPlanner().CreatePlanAsync(context, CancellationToken.None);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(7, action.MaxMessages);
        Assert.Equal(2, action.MaxConcurrency);
        Assert.Equal("smtp.example.test", Assert.Single(action.Targets));
        Assert.Equal("LOAD_ENGINE_AGENT", action.AgentId);
    }

    [Fact]
    public async Task Coordinator_Plans_Then_Authorizes()
    {
        var options = Options();
        var context = new AiTaskContext(options, new HashSet<string> { "smtp.example.test" });
        var registry = new MailLoadAgentRegistry(new[] { new StubAgent("LOAD_ENGINE_AGENT") });
        var coordinator = new AiExecutionCoordinator(
            new ConfiguredExecutionPlanner(),
            new AiSupervisor(registry, new AiActionGuard()));

        var actions = await coordinator.PrepareAsync(context, CancellationToken.None);

        Assert.Single(actions);
    }

    [Fact]
    public async Task Replanner_Reduces_Concurrency_On_Failures()
    {
        var options = Options(messageCount: 10, maxConcurrency: 4);
        var context = new AiTaskContext(options, new HashSet<string> { "smtp.example.test" });
        var registry = new MailLoadAgentRegistry(new[] { new StubAgent("LOAD_ENGINE_AGENT") });
        var coordinator = new AiExecutionCoordinator(
            new ConfiguredExecutionPlanner(),
            new AiSupervisor(registry, new AiActionGuard()),
            new ConservativeAiReplanner(),
            maxReplans: 2);

        var initial = await coordinator.PrepareAsync(context, CancellationToken.None);
        var result = new MailTestResult(
            Requested: 10,
            Sent: 6,
            Failed: 4,
            Elapsed: TimeSpan.FromSeconds(2),
            LastError: "temporary SMTP failure",
            AvgLatencyMs: 100,
            MinLatencyMs: 50,
            MaxLatencyMs: 200,
            P50LatencyMs: 90,
            P95LatencyMs: 180,
            P99LatencyMs: 200,
            ThroughputPerSec: 3,
            Smtp4xx: 2);

        var replanned = await coordinator.ReplanAsync(
            context, initial, result, replanOrdinal: 1, CancellationToken.None);

        var action = Assert.Single(replanned!);
        Assert.Equal(10, action.MaxMessages);
        Assert.Equal(2, action.MaxConcurrency);
        Assert.Equal("smtp.example.test", Assert.Single(action.Targets));
    }

    [Fact]
    public async Task Replanner_Never_Increases_Concurrency_Or_Message_Budget()
    {
        var options = Options(messageCount: 7, maxConcurrency: 3);
        var context = new AiTaskContext(options);
        var previous = new[]
        {
            new AiAction("a1", AiActionKind.LoadTest, "LOAD_ENGINE_AGENT",
                new[] { "smtp.example.test" }, 7, 3, 0)
        };
        var result = new MailTestResult(
            Requested: 7, Sent: 0, Failed: 7, Elapsed: TimeSpan.FromSeconds(1),
            LastError: "failure", AvgLatencyMs: 0, MinLatencyMs: 0, MaxLatencyMs: 0,
            P50LatencyMs: 0, P95LatencyMs: 0, P99LatencyMs: 0, ThroughputPerSec: 0,
            Timeouts: 1);

        var plan = await new ConservativeAiReplanner().CreateReplanAsync(
            new AiReplanContext(context, previous, result, 1), CancellationToken.None);

        var action = Assert.Single(plan!.Actions);
        Assert.True(action.MaxMessages <= previous[0].MaxMessages);
        Assert.True(action.MaxConcurrency <= previous[0].MaxConcurrency);
        Assert.Equal(previous[0].Targets, action.Targets);
    }

    [Fact]
    public async Task Replanner_Returns_No_Action_When_Run_Is_Healthy()
    {
        var options = Options();
        var context = new AiTaskContext(options);
        var previous = new[]
        {
            new AiAction("a1", AiActionKind.LoadTest, "LOAD_ENGINE_AGENT",
                new[] { "smtp.example.test" }, 10, 2, 0)
        };
        var result = new MailTestResult(
            Requested: 10, Sent: 10, Failed: 0, Elapsed: TimeSpan.FromSeconds(1),
            LastError: "", AvgLatencyMs: 50, MinLatencyMs: 20, MaxLatencyMs: 100,
            P50LatencyMs: 45, P95LatencyMs: 90, P99LatencyMs: 100, ThroughputPerSec: 10);

        var plan = await new ConservativeAiReplanner().CreateReplanAsync(
            new AiReplanContext(context, previous, result, 1), CancellationToken.None);

        Assert.Null(plan);
    }

    [Fact]
    public async Task Coordinator_Stops_After_Max_Replans()
    {
        var options = Options();
        var context = new AiTaskContext(options);
        var registry = new MailLoadAgentRegistry(new[] { new StubAgent("LOAD_ENGINE_AGENT") });
        var coordinator = new AiExecutionCoordinator(
            new ConfiguredExecutionPlanner(),
            new AiSupervisor(registry, new AiActionGuard()),
            new ConservativeAiReplanner(),
            maxReplans: 1);

        var previous = await coordinator.PrepareAsync(context, CancellationToken.None);
        var result = new MailTestResult(
            Requested: 10, Sent: 0, Failed: 10, Elapsed: TimeSpan.FromSeconds(1),
            LastError: "failure", AvgLatencyMs: 0, MinLatencyMs: 0, MaxLatencyMs: 0,
            P50LatencyMs: 0, P95LatencyMs: 0, P99LatencyMs: 0, ThroughputPerSec: 0,
            Timeouts: 1);

        var allowed = await coordinator.ReplanAsync(
            context, previous, result, 2, CancellationToken.None);

        Assert.Null(allowed);
    }


    private sealed record StubAgent(string Id) : IMailLoadAgent;
}
