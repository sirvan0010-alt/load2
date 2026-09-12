using Xunit;
using MailLoadTester;

namespace MailLoadTester.Tests;

public class ValidationTests
{
    static MailTestOptions Base(
        string from = "test@example.com",
        string[]? recipients = null,
        string host = "smtp.example.com",
        int port = 587,
        SmtpSecurity security = SmtpSecurity.StartTls,
        bool useAuth = false,
        string user = "",
        string pass = "",
        int count = 10,
        int interval = 0,
        bool batch = false,
        int batchSize = 5,
        int batchPause = 1,
        int concurrency = 1,
        string subject = "s",
        string body = "b",
        string name = "n",
        bool random = false,
        bool testMode = true,
        string domains = "example.com",
        bool html = false,
        string[]? attachments = null,
        IReadOnlyDictionary<string, string>? headers = null,
        bool ignoreCert = false,
        int retries = 1,
        bool dryRun = false)
        => new(from, recipients ?? new[] { "user@example.com" }, host, port, security, useAuth, user, pass,
            count, interval, batch, batchSize, batchPause, concurrency, subject, body, name, random, testMode, domains,
            html, attachments ?? Array.Empty<string>(), headers ?? new Dictionary<string, string>(), ignoreCert, retries, dryRun);

    [Theory]
    [InlineData("test@gmail.com")]
    [InlineData("abc.def+test@example.cz")]
    public void ValidEmailAccepted(string email) => Assert.True(Validation.IsValidEmail(email));

    [Theory]
    [InlineData("test")]
    [InlineData("@gmail.com")]
    [InlineData("")]
    public void InvalidEmailRejected(string email) => Assert.False(Validation.IsValidEmail(email));

    [Fact]
    public void RandomDataContainsTestId()
    {
        var x = RandomTestData.Create(42);
        Assert.Contains("42", x.Subject);
        Assert.Contains("42", x.Body);
    }

    [Fact]
    public void BatchModeRejectsZeroPause()
    {
        var o = Base(batch: true, batchPause: 0);
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void TestModeRejectsUnauthorizedRecipientDomain()
    {
        var o = Base(recipients: new[] { "user@other.example" }, domains: "example.com");
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void BatchModeAllowsFinalPartialBatch()
    {
        var o = Base(count: 12, batch: true, batchSize: 5, batchPause: 1);
        Validation.Validate(o);
    }

    [Fact]
    public void NonBatchModeDoesNotRequireBatchPause()
    {
        var o = Base(batch: false, batchPause: 0);
        Validation.Validate(o);
    }

    [Fact]
    public void MessageCountMustBePositive()
    {
        var o = Base(count: 0);
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void MaxConcurrencyMustBePositive()
    {
        var o = Base(concurrency: 0);
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void AuthRequiresCredentials()
    {
        var o = Base(useAuth: true, user: "", pass: "");
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void EmptyRecipientsRejected()
    {
        var o = Base(recipients: Array.Empty<string>());
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void InvalidFromRejected()
    {
        var o = Base(from: "not-an-email");
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void MaxRetriesRange()
    {
        var o = Base(retries: 99);
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void CustomHeadersMustBeXHeaders()
    {
        // SEC-002: reserved MIME names must not be injectable via CustomHeaders.
        var o = Base(headers: new Dictionary<string, string> { ["Subject"] = "bad" });
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
        // Valid X-* header is accepted.
        var ok = Base(headers: new Dictionary<string, string> { ["X-Test-Id"] = "42" });
        Validation.Validate(ok);
    }

    [Fact]
    public void CustomHeaderRejectsNewlineInjection()
    {
        var o = Base(headers: new Dictionary<string, string> { ["X-Test"] = "ok\r\nBcc: bad@example.com" });
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }

    [Fact]
    public void ExplainSmtpErrorContainsHintForTimeout()
    {
        var msg = Validation.ExplainSmtpError(new TimeoutException("timed out"));
        Assert.Contains("časový limit", msg);
    }

    [Fact]
    public void ExplainSmtpErrorFallsBackToMessage()
    {
        var msg = Validation.ExplainSmtpError(new InvalidOperationException("custom failure"));
        Assert.Equal("custom failure", msg);
    }
}
