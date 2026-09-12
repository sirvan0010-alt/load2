using System.Text;
using MailKit;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class ProtocolLogRedactionTests
{
    sealed class FixedSecretDetector : IAuthenticationSecretDetector
    {
        readonly int _start;
        readonly int _length;
        public FixedSecretDetector(int start, int length) { _start = start; _length = length; }
        public IList<AuthenticationSecret> DetectSecrets(byte[] buffer, int offset, int count) =>
            new List<AuthenticationSecret> { new AuthenticationSecret(_start, _length) };
    }

    [Fact]
    public void WithoutDetector_ReturnsPlainText()
    {
        var bytes = Encoding.ASCII.GetBytes("AUTH LOGIN\r\n");
        var text = ProtocolLogRedaction.DecodeClientOrServer(bytes, 0, bytes.Length, null);
        Assert.Equal("AUTH LOGIN", text);
    }

    [Fact]
    public void WithDetector_ReplacesSecretRangeWithStars()
    {
        var payload = "cGFzc3dvcmQxMjM=";
        var bytes = Encoding.ASCII.GetBytes(payload);
        var detector = new FixedSecretDetector(start: 0, length: bytes.Length);
        var text = ProtocolLogRedaction.DecodeClientOrServer(bytes, 0, bytes.Length, detector);
        Assert.DoesNotContain("cGFzc3dvcm", text);
        Assert.Equal(new string('*', bytes.Length), text);
    }

    [Fact]
    public void WithDetector_PreservesNonSecretPrefix()
    {
        var bytes = Encoding.ASCII.GetBytes("USER secret-value");
        var detector = new FixedSecretDetector(start: 5, length: "secret-value".Length);
        var text = ProtocolLogRedaction.DecodeClientOrServer(bytes, 0, bytes.Length, detector);
        Assert.StartsWith("USER ", text);
        Assert.DoesNotContain("secret-value", text);
        Assert.Contains('*', text);
    }

    [Fact]
    public void SessionProtocolLogger_WritesRedactedClientLine()
    {
        var path = Path.Combine(Path.GetTempPath(), "mlt-sec-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            using (var session = new SmtpSessionLogger(path))
            {
                var logger = new SessionProtocolLogger(session);
                var secret = Encoding.ASCII.GetBytes("SuperSecretPassword");
                logger.AuthenticationSecretDetector = new FixedSecretDetector(0, secret.Length);
                logger.LogClient(secret, 0, secret.Length);
            }

            // Dispose flushes; small settle for filesystem.
            for (var i = 0; i < 20; i++)
            {
                if (File.Exists(path) && File.ReadAllText(path).Contains('*'))
                    break;
                Thread.Sleep(50);
            }

            var content = File.ReadAllText(path);
            Assert.DoesNotContain("SuperSecretPassword", content);
            Assert.Contains('*', content);
            Assert.Contains("C: ", content);
        }
        finally
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }
    }
}
