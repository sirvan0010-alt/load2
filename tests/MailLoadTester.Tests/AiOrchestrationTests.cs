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

    private sealed record StubAgent(string Id) : IMailLoadAgent;
}
