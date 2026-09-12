using Xunit;

namespace MailLoadTester.Tests;

public sealed class AuthorizationGateTests
{
    static MailTestOptions Base(bool testMode = false, bool dryRun = false, bool unauthorized = false) => new(
        From: "from@example.com",
        Recipients: new[] { "to@example.com" },
        SmtpHost: "smtp.example.com",
        Port: 25,
        Security: SmtpSecurity.None,
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
        TestMode: testMode,
        AllowedDomains: testMode ? "example.com" : "",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 0,
        DryRun: dryRun,
        Unauthorized: unauthorized);

    [Fact]
    public void HasUnauthorizedFlag_DetectsSwitch()
    {
        Assert.True(AuthorizationGate.HasUnauthorizedFlag(new[] { "--unauthorized" }));
        Assert.True(AuthorizationGate.HasUnauthorizedFlag(new[] { "--foo", "--UNAUTHORIZED" }));
        Assert.False(AuthorizationGate.HasUnauthorizedFlag(new[] { "--help" }));
        Assert.False(AuthorizationGate.HasUnauthorizedFlag(null));
    }

    [Fact]
    public void DryRun_IsAlwaysAllowed()
    {
        AuthorizationGate.EnsureSendAuthorized(Base(dryRun: true, testMode: false, unauthorized: false));
    }

    [Fact]
    public void TestMode_IsAllowedWithoutUnauthorized()
    {
        AuthorizationGate.EnsureSendAuthorized(Base(testMode: true, unauthorized: false));
    }

    [Fact]
    public void RealSend_WithoutTestModeOrAck_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AuthorizationGate.EnsureSendAuthorized(Base(testMode: false, dryRun: false, unauthorized: false)));
    }

    [Fact]
    public void RealSend_WithUnauthorized_IsAllowed()
    {
        AuthorizationGate.EnsureSendAuthorized(Base(testMode: false, unauthorized: true));
    }

    [Fact]
    public void Validation_RejectsUnauthorizedMissingForLiveSend()
    {
        var o = Base(testMode: false, dryRun: false, unauthorized: false);
        var ex = Assert.Throws<ArgumentException>(() => Validation.Validate(o));
        Assert.Contains("unauthorized", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validation_AcceptsUnauthorizedForLiveSend()
    {
        Validation.Validate(Base(testMode: false, dryRun: false, unauthorized: true));
    }
}
