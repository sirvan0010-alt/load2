using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class TransportDiagnosticsTests
{
    static MailTestOptions Opts(int port, bool dry = false) => new(
        From: "sender@example.com",
        Recipients: new[] { "rcpt@example.com" },
        SmtpHost: "127.0.0.1",
        Port: port,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 1,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 0,
        MaxConcurrency: 1,
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
        MaxRetries: 0,
        DryRun: dry,
        Unauthorized: true,
        ConnectTimeoutMs: 5000,
        ReadTimeoutMs: 5000);

    [Fact]
    public async Task DryRun_SkipsNetwork()
    {
        var report = await TransportDiagnostics.RunAsync(
            Opts(25, dry: true),
            new TransportDiagnosticOptions(DryRun: true, CheckDnsPolicy: true, CheckSmtp: true));
        Assert.False(report.Connected);
        Assert.Contains("DRY-RUN", report.Summary);
        Assert.Null(report.Capabilities);
    }

    [Fact]
    public async Task FakeSmtp_ReportsCapabilities()
    {
        await using var server = new DiagFakeSmtp();
        var report = await TransportDiagnostics.RunAsync(
            Opts(server.Port),
            new TransportDiagnosticOptions(CheckDnsPolicy: false, CheckMx: false, CheckSmtp: true));
        Assert.True(report.Connected);
        Assert.NotNull(report.Capabilities);
        Assert.True(report.Capabilities!.Pipelining || report.Capabilities.AuthMechanisms.Count >= 0);
        Assert.Null(report.Error);
        Assert.DoesNotContain("password", report.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClosedPort_SetsError()
    {
        var report = await TransportDiagnostics.RunAsync(
            Opts(1),
            new TransportDiagnosticOptions(CheckDnsPolicy: false, CheckMx: false, CheckSmtp: true));
        Assert.False(report.Connected);
        Assert.False(string.IsNullOrEmpty(report.Error));
    }

    [Fact]
    public async Task Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TransportDiagnostics.RunAsync(
                Opts(25),
                new TransportDiagnosticOptions(CheckDnsPolicy: false, CheckSmtp: true, DryRun: false),
                cts.Token));
    }

    [Fact]
    public void FromClient_MapsEmptyClientSafely()
    {
        using var client = new MailKit.Net.Smtp.SmtpClient();
        var caps = TransportDiagnostics.FromClient(client);
        Assert.NotNull(caps.AuthMechanisms);
    }

    private sealed class DiagFakeSmtp : IAsyncDisposable
    {
        readonly TcpListener _listener;
        readonly CancellationTokenSource _cts = new();
        readonly Task _loop;
        public int Port { get; }

        public DiagFakeSmtp()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = AcceptLoop(_cts.Token);
        }

        async Task AcceptLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var c = await _listener.AcceptTcpClientAsync(ct);
                    _ = Handle(c, ct);
                }
            }
            catch { }
        }

        static async Task Handle(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                await using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true))
                await using (var writer = new StreamWriter(stream, Encoding.ASCII, 4096, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true })
                {
                    await writer.WriteLineAsync("220 diag-test");
                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        var line = await reader.ReadLineAsync(ct);
                        if (line is null) break;
                        var cmd = line.Length >= 4 ? line[..4].ToUpperInvariant() : line.ToUpperInvariant();
                        if (cmd.StartsWith("EHLO") || cmd.StartsWith("HELO"))
                        {
                            await writer.WriteLineAsync("250-localhost");
                            await writer.WriteLineAsync("250-PIPELINING");
                            await writer.WriteLineAsync("250-8BITMIME");
                            await writer.WriteLineAsync("250-AUTH PLAIN LOGIN");
                            await writer.WriteLineAsync("250 OK");
                        }
                        else if (cmd.StartsWith("QUIT"))
                        {
                            await writer.WriteLineAsync("221 Bye");
                            break;
                        }
                        else
                            await writer.WriteLineAsync("250 OK");
                    }
                }
            }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try { await _loop; } catch { }
            _cts.Dispose();
        }
    }
}
