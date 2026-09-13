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
