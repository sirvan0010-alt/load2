using Xunit;

namespace MailLoadTester.Tests;

public class SmtpTestRunnerQueueMetricsTests
{
    static MailTestOptions DryRunOptions(int messageCount, int maxConcurrency = 2) => new(
        From: "tester@example.com",
        Recipients: new[] { "user@example.com" },
        SmtpHost: "127.0.0.1",
        Port: 25,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: messageCount,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 1,
        MaxConcurrency: maxConcurrency,
        Subject: "Queue metrics dry-run",
        Body: "A3 integration",
        DisplayName: "MailLoadTester",
        RandomTestData: false,
        TestMode: true,
        AllowedDomains: "example.com",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 0,
        DryRun: true,
        Unauthorized: false);

    [Fact]
    public async Task DryRun_PopulatesQueueMetrics_MatchingMessageCount()
    {
        const int messageCount = 20;
        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(
            DryRunOptions(messageCount, maxConcurrency: 2),
            new Progress<ProgressUpdate>(),
            CancellationToken.None);

        Assert.NotNull(result.QueueMetrics);
        var q = result.QueueMetrics!;
        Assert.Equal(messageCount, q.Enqueued);
        Assert.Equal(messageCount, q.Dequeued);
        Assert.Equal(messageCount, q.Completed);
        Assert.Equal(0, q.QueueDepth);
        Assert.True(q.PeakQueueDepth >= 0);
        Assert.True(q.FullWaits >= 0);
        Assert.Equal(0, q.Cancelled);
        Assert.Equal(0, q.Drained);
    }

    [Fact]
    public async Task CancelledDryRun_QueueMetricsRemainConsistent()
    {
        const int messageCount = 500;
        var runner = new SmtpTestRunner();
        using var cts = new CancellationTokenSource();
        var runTask = runner.RunAsync(
            DryRunOptions(messageCount, maxConcurrency: 4),
            new Progress<ProgressUpdate>(),
            cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(30));

        var result = await runTask;

        Assert.NotNull(result.QueueMetrics);
        var q = result.QueueMetrics!;
        Assert.True(q.Enqueued >= q.Completed);
        Assert.True(q.Dequeued >= q.Completed);
        Assert.Equal(0, q.QueueDepth);
        Assert.True(q.Cancelled >= 0);
        Assert.True(q.Drained >= 0);
        Assert.True(q.Enqueued >= 0);
    }
}
