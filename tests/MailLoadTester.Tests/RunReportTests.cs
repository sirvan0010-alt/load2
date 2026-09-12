using Xunit;

namespace MailLoadTester.Tests;

public sealed class RunReportTests
{
    static MailTestOptions SampleOptions(string password = "SuperSecretPassword!") => new(
        From: "a@test.local",
        Recipients: new[] { "b@test.local", "c@test.local" },
        SmtpHost: "smtp.example",
        Port: 587,
        Security: SmtpSecurity.StartTls,
        UseAuthentication: true,
        Username: "user@example",
        Password: password,
        MessageCount: 10,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 0,
        MaxConcurrency: 2,
        Subject: "s",
        Body: "b",
        DisplayName: "n",
        RandomTestData: false,
        TestMode: true,
        AllowedDomains: "",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 1,
        DryRun: true,
        ProxyPassword: "proxy-secret",
        ClientCertificatePassword: "cert-secret",
        Unauthorized: false);

    static MailTestResult SampleResult(string? runId = null) => new(
        Requested: 10,
        Sent: 8,
        Failed: 2,
        Elapsed: TimeSpan.FromSeconds(1.5),
        LastError: "x",
        AvgLatencyMs: 12,
        MinLatencyMs: 1,
        MaxLatencyMs: 40,
        P50LatencyMs: 10,
        P95LatencyMs: 30,
        P99LatencyMs: 35,
        ThroughputPerSec: 5,
        RunId: runId);

    [Fact]
    public void ToJson_IsDeterministic_ForSameReport()
    {
        var opts = SampleOptions();
        var health = new[]
        {
            new EndpointHealthSnapshot("b:25", EndpointHealthState.Healthy, 0, 1, 0, null, null),
            new EndpointHealthSnapshot("a:25", EndpointHealthState.Degraded, 2, 0, 2, null, "timeout"),
        };
        var report = RunReportBuilder.Create(
            "run-fixed-id",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 3, 5, 5, TimeSpan.Zero),
            opts,
            SampleResult("run-fixed-id"),
            health);

        var j1 = RunReportBuilder.ToJson(report);
        var j2 = RunReportBuilder.ToJson(report);
        Assert.Equal(j1, j2);
        Assert.True(j1.IndexOf("a:25", StringComparison.Ordinal) < j1.IndexOf("b:25", StringComparison.Ordinal));
    }

    [Fact]
    public void ToJson_DoesNotContainSecrets()
    {
        var opts = SampleOptions("SuperSecretPassword!");
        var report = RunReportBuilder.Create(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            opts,
            SampleResult());
        var json = RunReportBuilder.ToJson(report);
        Assert.False(RunReportBuilder.JsonContainsSecretMaterial(json));
        Assert.DoesNotContain("SuperSecretPassword", json, StringComparison.Ordinal);
        Assert.DoesNotContain("proxy-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("cert-secret", json, StringComparison.Ordinal);
        Assert.Contains("user@example", json);
        Assert.Contains("smtp.example", json);
    }

    [Fact]
    public void Create_EmptyRun_StillSerializes()
    {
        var opts = SampleOptions() with { MessageCount = 0, UseAuthentication = false, Username = "", Password = "" };
        var result = new MailTestResult(0, 0, 0, TimeSpan.Zero, "", 0, 0, 0, 0, 0, 0, 0);
        var report = RunReportBuilder.Create("empty", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, opts, result);
        var json = RunReportBuilder.ToJson(report);
        Assert.Contains("\"runId\": \"empty\"", json);
        Assert.Contains("\"sent\": 0", json);
    }

    [Fact]
    public void Create_AttachesRunId_ToResult()
    {
        var report = RunReportBuilder.Create(
            "abc123",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            SampleOptions(),
            SampleResult(null));
        Assert.Equal("abc123", report.Result.RunId);
        Assert.Equal("abc123", report.RunId);
    }

    [Fact]
    public async Task DryRun_Runner_PopulatesRunId()
    {
        await using var port = new LocalPort();
        var options = SampleOptions() with
        {
            SmtpHost = "127.0.0.1",
            Port = port.Port,
            MessageCount = 2,
            UseAuthentication = false,
            Username = "",
            Password = "",
            DryRun = true,
            TestMode = false,
            Unauthorized = true
        };
        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(options, new Progress<ProgressUpdate>(), CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(result.RunId));
        Assert.NotNull(result.EndpointHealth);
        var report = RunReportBuilder.Create(
            result.RunId!,
            DateTimeOffset.UtcNow.AddSeconds(-1),
            DateTimeOffset.UtcNow,
            options,
            result,
            result.EndpointHealth);
        var json = RunReportBuilder.ToJson(report);
        Assert.False(RunReportBuilder.JsonContainsSecretMaterial(json));
        Assert.Contains(result.RunId!, json);
    }

    private sealed class LocalPort : IAsyncDisposable
    {
        private readonly System.Net.Sockets.TcpListener _l;
        public int Port { get; }
        public LocalPort()
        {
            _l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            _l.Start();
            Port = ((System.Net.IPEndPoint)_l.LocalEndpoint).Port;
        }
        public ValueTask DisposeAsync()
        {
            _l.Stop();
            return ValueTask.CompletedTask;
        }
    }
}
