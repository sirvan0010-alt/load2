using Xunit;

namespace MailLoadTester.Tests;

public sealed class TimingBreakdownTests
{
    [Fact]
    public async Task DryRun_PopulatesPhaseTimingAverages()
    {
        // Dry-run still goes through WaitBeforeSend + adaptive path; pool/smtp stay 0.
        await using var server = new LocalUnusedPort();
        var options = new MailTestOptions(
            From: "a@test.local",
            Recipients: new[] { "b@test.local" },
            SmtpHost: "127.0.0.1",
            Port: server.Port,
            Security: SmtpSecurity.None,
            UseAuthentication: false,
            Username: "",
            Password: "",
            MessageCount: 3,
            IntervalMs: 5,
            BatchMode: false,
            BatchSize: 1,
            BatchPauseSeconds: 0,
            MaxConcurrency: 2,
            Subject: "s",
            Body: "b",
            DisplayName: "n",
            RandomTestData: false,
            TestMode: false,
            AllowedDomains: "",
            HtmlBody: false,
            Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(),
            IgnoreCertificateErrors: false,
            MaxRetries: 0,
            DryRun: true,
            UseAdaptiveConcurrency: true);

        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(options, new Progress<ProgressUpdate>(), CancellationToken.None);

        Assert.Equal(3, result.Sent);
        Assert.True(result.AvgPrepWaitMs >= 0);
        Assert.True(result.AvgAdaptiveWaitMs >= 0);
        Assert.Equal(0, result.AvgPoolWaitMs);
        Assert.Equal(0, result.AvgSmtpSendMs);
        Assert.True(result.AvgLatencyMs >= 0);
    }

    private sealed class LocalUnusedPort : IAsyncDisposable
    {
        private readonly System.Net.Sockets.TcpListener _listener;
        public int Port { get; }
        public LocalUnusedPort()
        {
            _listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port;
        }
        public ValueTask DisposeAsync()
        {
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }
}
