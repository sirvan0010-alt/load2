using System.Net;
using System.Net.Sockets;
using System.Text;
using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmtpCapabilityDiscoveryTests
{
    [Fact]
    public async Task ProbeSubmissionEndpoint_ReadsSizeAndEhloCapabilitiesWithoutSendingMail()
    {
        await using var server = new CapabilitySmtpServer();
        var service = new SmtpCapabilityDiscoveryService();

        var result = await service.ProbeSubmissionEndpointAsync(
            "127.0.0.1", server.Port, SmtpSecurity.None, timeoutMs: 5000);

        Assert.True(result.Connected);
        Assert.Equal(52_428_800L, result.MaxMessageBytes);
        Assert.True(result.Pipelining);
        Assert.True(result.EightBitMime);
        Assert.True(result.SmtpUtf8);
        Assert.True(result.Chunking);
        Assert.Contains("PLAIN", result.AuthMechanisms);
        Assert.Null(result.Error);
        Assert.Equal(0, server.MailCommandCount);
        Assert.Equal(0, server.DataCommandCount);
    }

    [Fact]
    public void EstimateBase64EncodedBytes_IncludesMimeLineWrapping()
    {
        Assert.Equal(4L, MailboxCapacityMath.EstimateBase64EncodedBytes(1));
        Assert.Equal(8L, MailboxCapacityMath.EstimateBase64EncodedBytes(4));
        Assert.True(MailboxCapacityMath.EstimateBase64EncodedBytes(1024L * 1024) > 1_398_101);
    }

    [Fact]
    public void EstimateMimeMessageBytes_IncludesNonAttachmentOverhead()
    {
        var attachment = 1024L * 1024 * 1024;
        var estimate = MailboxCapacityMath.EstimateMimeMessageBytes(attachment);
        Assert.True(estimate > 1_430_000_000);
        Assert.True(estimate < 1_440_000_000);
    }

    [Fact]
    public void EstimateMaxBinaryAttachmentBytes_StaysWithinAdvertisedMessageLimit()
    {
        const long serverLimit = 25L * 1024 * 1024;
        var maxAttachment = MailboxCapacityMath.EstimateMaxBinaryAttachmentBytes(serverLimit);
        Assert.NotNull(maxAttachment);
        Assert.True(MailboxCapacityMath.EstimateMimeMessageBytes(maxAttachment!.Value) <= serverLimit);
        Assert.True(MailboxCapacityMath.EstimateMimeMessageBytes(maxAttachment.Value + 1) > serverLimit);
    }

    [Fact]
    public void EstimateMaxBinaryAttachmentBytes_IsUnknownWithoutServerLimit()
    {
        Assert.Null(MailboxCapacityMath.EstimateMaxBinaryAttachmentBytes(null));
    }

    [Fact]
    public void EstimateBase64EncodedBytes_RejectsNegativeAndOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MailboxCapacityMath.EstimateBase64EncodedBytes(-1));
        Assert.Throws<OverflowException>(() => MailboxCapacityMath.EstimateBase64EncodedBytes(long.MaxValue));
    }

    private sealed class CapabilitySmtpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        public int Port { get; }
        private int _mailCommandCount;
        private int _dataCommandCount;
        public int MailCommandCount => Volatile.Read(ref _mailCommandCount);
        public int DataCommandCount => Volatile.Read(ref _dataCommandCount);

        public CapabilitySmtpServer()
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = AcceptLoopAsync(_cts.Token);
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                    _ = HandleAsync(client, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                using (client)
                await using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true))
                await using (var writer = new StreamWriter(stream, Encoding.ASCII, 4096, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                })
                {
                    await writer.WriteLineAsync("220 test.local ESMTP ready");
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(cancellationToken);
                        if (line is null) break;
                        var command = line.Split(' ', 2)[0].ToUpperInvariant();
                        switch (command)
                        {
                            case "EHLO":
                                await writer.WriteLineAsync("250-test.local");
                                await writer.WriteLineAsync("250-SIZE 52428800");
                                await writer.WriteLineAsync("250-PIPELINING");
                                await writer.WriteLineAsync("250-8BITMIME");
                                await writer.WriteLineAsync("250-SMTPUTF8");
                                await writer.WriteLineAsync("250-CHUNKING");
                                await writer.WriteLineAsync("250-AUTH PLAIN LOGIN");
                                await writer.WriteLineAsync("250 OK");
                                break;
                            case "QUIT":
                                await writer.WriteLineAsync("221 Bye");
                                return;
                            case "MAIL":
                                Interlocked.Increment(ref _mailCommandCount);
                                await writer.WriteLineAsync("250 OK");
                                break;
                            case "DATA":
                                Interlocked.Increment(ref _dataCommandCount);
                                await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                                break;
                            default:
                                await writer.WriteLineAsync("250 OK");
                                break;
                        }
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
