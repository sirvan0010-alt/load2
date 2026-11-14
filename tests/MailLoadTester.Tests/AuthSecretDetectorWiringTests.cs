using MailKit.Net.Smtp;
using Xunit;

namespace MailLoadTester.Tests;

/// <summary>
/// SEC-AUDIT-001 follow-up: MailKit SmtpClient(IProtocolLogger) must attach
/// AuthenticationSecretDetector so SessionProtocolLogger can redact AUTH material.
/// Redaction behavior itself is covered by ProtocolLogRedactionTests with a
/// deterministic detector; an arbitrary raw client payload is not guaranteed to
/// be classified as authentication material by MailKit's detector.
/// </summary>
public sealed class AuthSecretDetectorWiringTests
{
    [Fact]
    public async Task SmtpClient_AuthLogin_RedactsPasswordFromRealSessionLog()
    {
        const string username = "mlt-test-user";
        const string password = "SuperSecretPassword-DoNotLog";
        var path = Path.Combine(Path.GetTempPath(), "mlt-auth-e2e-" + Guid.NewGuid().ToString("N") + ".log");
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
                using var stream = socket.GetStream();
                using var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, leaveOpen: true);
                using var writer = new StreamWriter(stream, System.Text.Encoding.ASCII, 1024, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };

                await writer.WriteLineAsync("220 localhost ESMTP test");
                while (!timeout.IsCancellationRequested)
                {
                    var command = await reader.ReadLineAsync(timeout.Token);
                    if (command is null) break;

                    if (command.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250-localhost");
                        await writer.WriteLineAsync("250-AUTH LOGIN");
                        await writer.WriteLineAsync("250 SIZE 1048576");
                    }
                    else if (command.Equals("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("334 VXNlcm5hbWU6");
                    }
                    else if (command == Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(username)))
                    {
                        await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                    }
                    else if (command == Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password)))
                    {
                        await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                    }
                    else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 Bye");
                        break;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unexpected SMTP command in AUTH integration test: " + command);
                    }
                }
            }, timeout.Token);

            using (var session = new SmtpSessionLogger(path))
            using (var client = new SmtpClient(new SessionProtocolLogger(session)))
            {
                await client.ConnectAsync("127.0.0.1", port, MailKit.Security.SecureSocketOptions.None, timeout.Token);
                await client.AuthenticateAsync(username, password, timeout.Token);
                await client.DisconnectAsync(true, timeout.Token);
            }

            await serverTask;
            var log = File.ReadAllText(path);
            Assert.DoesNotContain(password, log);
            Assert.DoesNotContain(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password)), log);
            Assert.Contains("AUTH LOGIN", log);
        }
        finally
        {
            listener.Stop();
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void SmtpClient_WithSessionProtocolLogger_WiresAuthenticationSecretDetector()
    {
        var path = Path.Combine(Path.GetTempPath(), "mlt-auth-wire-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            using var session = new SmtpSessionLogger(path);
            var logger = new SessionProtocolLogger(session);
            Assert.Null(logger.AuthenticationSecretDetector);

            using (var client = new SmtpClient(logger))
            {
                Assert.NotNull(logger.AuthenticationSecretDetector);
            }
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }
}
