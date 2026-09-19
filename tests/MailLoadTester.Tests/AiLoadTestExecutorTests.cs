using Xunit;
using MailLoadTester;

namespace MailLoadTester.Tests;

public sealed class AiLoadTestExecutorTests
{
    [Fact]
    public void Executor_Rejects_Non_Load_Action()
    {
        var executor = new AiLoadTestExecutor(new SmtpTestRunner());
        var action = new AiAction(
            "a1",
            AiActionKind.Diagnostics,
            "LOAD_ENGINE_AGENT",
            new[] { "smtp.example.test" },
            1,
            1,
            0);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(
                action,
                new AiTaskContext(CreateOptions()),
                new Progress<ProgressUpdate>(),
                CancellationToken.None));
    }

    [Fact]
    public async Task Executor_Rejects_Cancelled_Run_Before_Network_Execution()
    {
        var executor = new AiLoadTestExecutor(new SmtpTestRunner());
        var action = new AiAction(
            "a1",
            AiActionKind.LoadTest,
            "LOAD_ENGINE_AGENT",
            new[] { "smtp.example.test" },
            1,
            1,
            1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(
                action,
                new AiTaskContext(CreateOptions()),
                new Progress<ProgressUpdate>(),
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
}
