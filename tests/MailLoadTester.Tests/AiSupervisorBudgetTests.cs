using Xunit;
using MailLoadTester;

namespace MailLoadTester.Tests;

public sealed class AiSupervisorBudgetTests
{
    [Fact]
    public async Task Supervisor_Rejects_Aggregate_Message_Budget_Overrun()
    {
        var options = CreateOptions();
        var context = new AiTaskContext(options);
        var registry = new MailLoadAgentRegistry(new[] { new TestAgent() });
        var supervisor = new AiSupervisor(registry, new AiActionGuard());

        var plan = new ExecutionPlan(new[]
        {
            new AiAction("a1", AiActionKind.LoadTest, "LOAD_ENGINE_AGENT",
                new[] { "smtp.example.test" }, 6, 1, 0),
            new AiAction("a2", AiActionKind.LoadTest, "LOAD_ENGINE_AGENT",
                new[] { "smtp.example.test" }, 6, 1, 0)
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            supervisor.AuthorizePlanAsync(plan, context, CancellationToken.None).AsTask());
    }

    private static MailTestOptions CreateOptions() =>
        new(
            From: "tester@example.test",
            Recipients: new[] { "recipient@example.test" },
            SmtpHost: "smtp.example.test",
            Port: 587,
            Security: SmtpSecurity.StartTls,
            UseAuthentication: false,
            Username: "",
            Password: "",
            MessageCount: 10,
            IntervalMs: 100,
            BatchMode: false,
            BatchSize: 1,
            BatchPauseSeconds: 1,
            MaxConcurrency: 2,
            Subject: "test",
            Body: "test",
            DisplayName: "test",
            RandomTestData: false,
            TestMode: true,
            AllowedDomains: "example.test",
            HtmlBody: false,
            Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(),
            IgnoreCertificateErrors: false,
            MaxRetries: 0,
            DryRun: false,
            Unauthorized: false);

    private sealed class TestAgent : IMailLoadAgent
    {
        public string Id => "LOAD_ENGINE_AGENT";
    }
}
