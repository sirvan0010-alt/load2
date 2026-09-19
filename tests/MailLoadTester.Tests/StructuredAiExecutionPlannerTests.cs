using Xunit;
using MailLoadTester;

namespace MailLoadTester.Tests;

public sealed class StructuredAiExecutionPlannerTests
{
    [Fact]
    public async Task Planner_Parses_And_Validates_Structured_Plan()
    {
        var provider = new StubProvider("""
        {
          "actions": [
            {
              "actionId": "a1",
              "kind": "LoadTest",
              "agentId": "LOAD_ENGINE_AGENT",
              "targets": ["smtp.example.test"],
              "maxMessages": 5,
              "maxConcurrency": 2,
              "maxDurationSeconds": 30
            }
          ]
        }
        """);

        var options = new MailTestOptions(
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

        var plan = await new StructuredAiExecutionPlanner(provider)
            .CreatePlanAsync(new AiTaskContext(options), CancellationToken.None);

        var action = Assert.Single(plan.Actions);
        Assert.Equal(AiActionKind.LoadTest, action.Kind);
        Assert.Equal(5, action.MaxMessages);
    }

    [Fact]
    public async Task Planner_Rejects_Unknown_Json_Fields()
    {
        var provider = new StubProvider("""
        {
          "actions": [],
          "unexpected": true
        }
        """);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new StructuredAiExecutionPlanner(provider)
                .CreatePlanAsync(
                    new AiTaskContext(CreateOptions()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Planner_Rejects_Invalid_Action_Bounds()
    {
        var provider = new StubProvider("""
        {
          "actions": [
            {
              "actionId": "a1",
              "kind": "LoadTest",
              "agentId": "LOAD_ENGINE_AGENT",
              "targets": ["smtp.example.test"],
              "maxMessages": 10001,
              "maxConcurrency": 2,
              "maxDurationSeconds": 30
            }
          ]
        }
        """);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await new StructuredAiExecutionPlanner(provider)
                .CreatePlanAsync(
                    new AiTaskContext(CreateOptions()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Planner_Propagates_Cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var provider = new StubProvider("{}");

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new StructuredAiExecutionPlanner(provider)
                .CreatePlanAsync(
                    new AiTaskContext(CreateOptions()),
                    cts.Token));
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

    private sealed class StubProvider(string json) : IStructuredAiPlanProvider
    {
        public ValueTask<string> CreatePlanJsonAsync(
            AiTaskContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(json);
        }
    }
}
