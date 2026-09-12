using MailKit.Security;
using Xunit;

namespace MailLoadTester.Tests;

/// <summary>NET-AUDIT-002 — TLS mode ↔ socket options and validation matrix (no live network).</summary>
public sealed class TlsMatrixTests
{
    static MailTestOptions Opts(
        SmtpSecurity security = SmtpSecurity.None,
        int port = 25,
        bool dryRun = false,
        bool ignoreCert = false) => new(
        From: "from@example.com",
        Recipients: new[] { "to@example.com" },
        SmtpHost: "smtp.example.invalid",
        Port: port,
        Security: security,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 1,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 1,
        MaxConcurrency: 1,
        Subject: "s",
        Body: "b",
        DisplayName: "n",
        RandomTestData: false,
        TestMode: false,
        AllowedDomains: "",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: ignoreCert,
        MaxRetries: 0,
        DryRun: dryRun);

    [Theory]
    [InlineData(SmtpSecurity.None, SecureSocketOptions.None)]
    [InlineData(SmtpSecurity.StartTls, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurity.ImplicitTls, SecureSocketOptions.SslOnConnect)]
    public void ToSocketOptions_MapsEachSecurityMode(SmtpSecurity security, SecureSocketOptions expected)
    {
        Assert.Equal(expected, SmtpConnectivityTester.ToSocketOptions(security));
    }

    [Fact]
    public void Validation_ImplicitTls_RequiresPort465()
    {
        var bad = Opts(SmtpSecurity.ImplicitTls, port: 587);
        Assert.Throws<ArgumentException>(() => Validation.Validate(bad));

        var good = Opts(SmtpSecurity.ImplicitTls, port: 465);
        Validation.Validate(good);
    }

    [Fact]
    public void Validation_StartTls_RejectsPort465()
    {
        var bad = Opts(SmtpSecurity.StartTls, port: 465);
        Assert.Throws<ArgumentException>(() => Validation.Validate(bad));

        var good = Opts(SmtpSecurity.StartTls, port: 587);
        Validation.Validate(good);
    }

    [Fact]
    public void Validation_Plain_AcceptsCommonPorts()
    {
        Validation.Validate(Opts(SmtpSecurity.None, port: 25));
        Validation.Validate(Opts(SmtpSecurity.None, port: 2525));
    }

    [Fact]
    public async Task DryRun_TestAsync_ReturnsWithoutNetwork()
    {
        var o = Opts(dryRun: true, security: SmtpSecurity.StartTls, port: 587);
        var msg = await SmtpConnectivityTester.TestAsync(o, CancellationToken.None);
        Assert.Contains("DRY-RUN", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("smtp.example.invalid", msg);
    }
}
