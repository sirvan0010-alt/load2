using MailKit.Net.Smtp;
using Xunit;

namespace MailLoadTester.Tests;

/// <summary>
/// SEC-AUDIT-001 follow-up: MailKit SmtpClient(IProtocolLogger) must attach
/// AuthenticationSecretDetector so SessionProtocolLogger can redact AUTH material.
/// </summary>
public sealed class AuthSecretDetectorWiringTests
{
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

    [Fact]
    public void WiredDetector_RedactsClientBytesInSessionLog()
    {
        var path = Path.Combine(Path.GetTempPath(), "mlt-auth-redact-" + Guid.NewGuid().ToString("N") + ".log");
        const string secret = "SuperSecretAuthToken";
        try
        {
            using (var session = new SmtpSessionLogger(path))
            {
                var logger = new SessionProtocolLogger(session);
                using var client = new SmtpClient(logger);
                Assert.NotNull(logger.AuthenticationSecretDetector);

                var bytes = System.Text.Encoding.ASCII.GetBytes(secret);
                logger.LogClient(bytes, 0, bytes.Length);
            }

            for (var i = 0; i < 20; i++)
            {
                if (File.Exists(path) && File.ReadAllText(path).Length > 0)
                    break;
                Thread.Sleep(50);
            }

            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("C: ", content);
            Assert.DoesNotContain(secret, content, StringComparison.Ordinal);
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }
}
