using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class ConnectionChurnTests
{
    static MailTestOptions BaseOpts(int port) => new(
        From: "a@test.local",
        Recipients: new[] { "b@test.local" },
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
        MaxRetries: 0,
        DryRun: false,
        Unauthorized: true,
        ConnectTimeoutMs: 5000,
        ReadTimeoutMs: 5000);

    [Fact]
    public void Validate_RejectsInvalidCycles()
    {
        Assert.Throws<ArgumentException>(() =>
            ConnectionChurnRunner.Validate(new ConnectionChurnOptions(MaxCycles: 0)));
    }

    [Fact]
    public async Task DryRun_SingleCycle()
    {
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1) with { DryRun = true },
            new ConnectionChurnOptions(MaxCycles: 1, DryRun: true),
            CancellationToken.None);
        Assert.Equal(1, result.CompletedCycles);
        Assert.Equal(1, result.ConnectSuccesses);
        Assert.Equal(0, result.ConnectFailures);
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task DryRun_BoundedMultipleCycles()
    {
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1),
            new ConnectionChurnOptions(MaxCycles: 25, MaxConcurrency: 4, DryRun: true),
            CancellationToken.None);
        Assert.Equal(25, result.CompletedCycles);
        Assert.Equal(25, result.ConnectSuccesses);
        Assert.Equal(25, result.Disconnects);
    }

    [Fact]
    public async Task DryRun_PersistentBaseline_NoDisconnectCount()
    {
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1),
            new ConnectionChurnOptions(MaxCycles: 5, ForceNewConnectionEachCycle: false, DryRun: true),
            CancellationToken.None);
        Assert.Equal(5, result.ConnectSuccesses);
        Assert.Equal(0, result.Disconnects);
    }

    [Fact]
    public async Task Cancellation_StopsEarly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1),
            new ConnectionChurnOptions(MaxCycles: 1000, DryRun: true),
            cts.Token);
        Assert.True(result.Cancelled || result.CompletedCycles < 1000);
    }

    [Fact]
    public async Task DurationTimeout_SetsTimedOut()
    {
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1),
            new ConnectionChurnOptions(MaxCycles: 100_000, MaxDurationSeconds: 1, MaxConcurrency: 2, DryRun: true),
            CancellationToken.None);
        Assert.True(result.Elapsed.TotalSeconds < 15);
        Assert.True(result.TimedOut || result.CompletedCycles > 0);
    }

    [Fact]
    public async Task RealPool_ChurnCreatesNewConnections()
    {
        await using var server = new ChurnFakeSmtp();
        var opts = BaseOpts(server.Port);
        var result = await ConnectionChurnRunner.RunAsync(
            opts,
            new ConnectionChurnOptions(MaxCycles: 3, MaxConcurrency: 1, ForceNewConnectionEachCycle: true),
            CancellationToken.None);
        Assert.Equal(3, result.ConnectSuccesses);
        Assert.Equal(3, result.Disconnects);
        Assert.True(result.PoolCreatedCount >= 3, $"expected ≥3 created, got {result.PoolCreatedCount}");
        Assert.False(result.Cancelled);
    }

    [Fact]
    public async Task RealPool_PersistentReusesConnection()
    {
        await using var server = new ChurnFakeSmtp();
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(server.Port),
            new ConnectionChurnOptions(MaxCycles: 3, MaxConcurrency: 1, ForceNewConnectionEachCycle: false),
            CancellationToken.None);
        Assert.Equal(3, result.ConnectSuccesses);
        Assert.Equal(0, result.Disconnects);
        Assert.Equal(1, result.PoolCreatedCount);
    }

    [Fact]
    public async Task Failure_AgainstClosedPort_ClassifiesFailures()
    {
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1),
            new ConnectionChurnOptions(MaxCycles: 2, MaxConcurrency: 1, ForceNewConnectionEachCycle: true),
            CancellationToken.None);
        Assert.True(result.ConnectFailures >= 1 || result.CompletedCycles >= 1);
        Assert.True(result.ConnectSuccesses == 0);
    }

    [Fact]
    public async Task MultiAccount_DryRun_Completes()
    {
        var accounts = new[]
        {
            new SmtpAccount("a", "127.0.0.1", 25, SmtpSecurity.None, false, "", ""),
            new SmtpAccount("b", "127.0.0.1", 26, SmtpSecurity.None, false, "", "")
        };
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1) with { Accounts = accounts },
            new ConnectionChurnOptions(MaxCycles: 4, MaxConcurrency: 2, DryRun: true),
            CancellationToken.None);
        Assert.Equal(4, result.ConnectSuccesses);
    }

    [Fact]
    public async Task ConcurrentChurn_DryRun_IsBounded()
    {
        var result = await ConnectionChurnRunner.RunAsync(
            BaseOpts(1),
            new ConnectionChurnOptions(MaxCycles: 40, MaxConcurrency: 8, DryRun: true),
            CancellationToken.None);
        Assert.Equal(40, result.CompletedCycles);
        Assert.Equal(40, result.ConnectSuccesses);
    }

    private sealed class ChurnFakeSmtp : IAsyncDisposable
    {
        readonly TcpListener _listener;
        readonly CancellationTokenSource _cts = new();
        readonly Task _loop;
        public int Port { get; }

        public ChurnFakeSmtp()
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
                    var client = await _listener.AcceptTcpClientAsync(ct);
                    _ = Handle(client, ct);
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
                    await writer.WriteLineAsync("220 churn-test");
                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        var line = await reader.ReadLineAsync(ct);
                        if (line is null) break;
                        var cmd = line.Length >= 4 ? line[..4].ToUpperInvariant() : line.ToUpperInvariant();
                        if (cmd.StartsWith("EHLO") || cmd.StartsWith("HELO"))
                        {
                            await writer.WriteLineAsync("250-localhost");
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
