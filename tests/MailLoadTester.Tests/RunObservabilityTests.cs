using Xunit;

namespace MailLoadTester.Tests;

public class RunObservabilityTests
{
    static MailTestResult SampleResult() => new(
        Requested: 100,
        Sent: 90,
        Failed: 10,
        Elapsed: TimeSpan.FromSeconds(10),
        LastError: "x",
        AvgLatencyMs: 50,
        MinLatencyMs: 10,
        MaxLatencyMs: 200,
        P50LatencyMs: 40,
        P95LatencyMs: 100,
        P99LatencyMs: 180,
        ThroughputPerSec: 9,
        Retries: 5,
        Smtp4xx: 3,
        Smtp5xx: 2,
        Timeouts: 1,
        AvgPrepWaitMs: 1,
        AvgAdaptiveWaitMs: 2,
        AvgPoolWaitMs: 3,
        AvgPaceWaitMs: 4,
        AvgSmtpSendMs: 5,
        RunId: "abc",
        QueueMetrics: new ScenarioQueueMetricsSnapshot(0, 8, 100, 100, 90, 0, 0, 2),
        RetryMetrics: new RetryMetricsSnapshot(3, 5, 1, 1, 2, 0, new Dictionary<int, int> { [1] = 3 }),
        OutcomeCounts: new SmtpOutcomeCountsSnapshot(90, 3, 1, 1, 0, 0, 2, 2, 0, 1));

    [Fact]
    public void FromResult_projects_A3_A4_A5_and_success_rate()
    {
        var s = RunObservability.FromResult(SampleResult());
        Assert.Equal("abc", s.RunId);
        Assert.Equal(90.0, s.SuccessRatePct);
        Assert.NotNull(s.Queue);
        Assert.Equal(8, s.Queue!.PeakQueueDepth);
        Assert.NotNull(s.Retry);
        Assert.Equal(5, s.Retry!.RetryAttempts);
        Assert.NotNull(s.Outcomes);
        Assert.Equal(90, s.Outcomes!.Success);
        Assert.Equal(5, s.AvgSmtpSendMs);
    }

    [Fact]
    public void FromReport_prefers_ledger_counts()
    {
        var result = SampleResult();
        var report = RunReportBuilder.Create(
            "run1",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            MinimalOptions(),
            result,
            ledgerAccepted: 88,
            ledgerFailed: 12);
        var s = RunObservability.FromReport(report);
        Assert.Equal("run1", s.RunId);
        Assert.Equal(88, s.Sent);
        Assert.Equal(12, s.Failed);
        Assert.Equal(88.0, s.SuccessRatePct);
    }

    [Fact]
    public void RunReport_json_has_no_secret_keys()
    {
        var o = MinimalOptions() with { Password = "secret-pass", ProxyPassword = "prox-secret" };
        var report = RunReportBuilder.Create(
            "r2", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, o, SampleResult());
        var json = RunReportBuilder.ToJson(report);
        Assert.False(RunReportBuilder.JsonContainsSecretMaterial(json));
        Assert.DoesNotContain("secret-pass", json);
        Assert.Contains("outcomeCounts", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatSummaryLine_includes_core_counters()
    {
        var line = RunObservability.FormatSummaryLine(RunObservability.FromResult(SampleResult()));
        Assert.Contains("sent=90/100", line);
        Assert.Contains("retries=5", line);
    }

    static MailTestOptions MinimalOptions() => new(
        From: "a@example.com",
        Recipients: new[] { "b@example.com" },
        SmtpHost: "smtp.example.com",
        Port: 587,
        Security: SmtpSecurity.StartTls,
        UseAuthentication: true,
        Username: "user",
        Password: "",
        MessageCount: 1,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 1,
        MaxConcurrency: 2,
        Subject: "t",
        Body: "b",
        DisplayName: "t",
        RandomTestData: false,
        TestMode: true,
        AllowedDomains: "example.com",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: true,
        MaxRetries: 0,
        DryRun: true);
}
