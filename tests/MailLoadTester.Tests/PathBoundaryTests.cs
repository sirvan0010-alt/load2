using Xunit;

namespace MailLoadTester.Tests;

/// <summary>SEC-AUDIT-002 partial — reject path traversal segments on validated paths.</summary>
public sealed class PathBoundaryTests
{
    static MailTestOptions Base(string? eml = null, string? sessionLog = null) => new(
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
        TestMode: false,
        AllowedDomains: "",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 0,
        DryRun: true,
        EmlTemplatePath: eml,
        SessionLogPath: sessionLog ?? "",
        EnableSessionLog: !string.IsNullOrEmpty(sessionLog));

    [Theory]
    [InlineData("../secret.eml")]
    [InlineData("foo/../../etc/passwd")]
    [InlineData("..\\windows\\system32")]
    public void EmlPath_RejectsDotDot(string path)
    {
        var o = Base(eml: path);
        var ex = Assert.Throws<ArgumentException>(() => Validation.Validate(o));
        Assert.Contains("..", ex.Message);
    }

    [Theory]
    [InlineData("../out.log")]
    [InlineData("logs/../../x.log")]
    public void SessionLogPath_RejectsDotDot(string path)
    {
        var o = Base(sessionLog: path);
        var ex = Assert.Throws<ArgumentException>(() => Validation.Validate(o));
        Assert.Contains("..", ex.Message);
    }

    [Fact]
    public async Task ProfileStore_RejectsDotDotPath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => ProfileStore.LoadAsync("../evil.json"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProfileStore.SaveAsync("../evil.json", Base(), includeSecrets: false));
    }

    [Fact]
    public void ContainsPathTraversal_DetectsSegments()
    {
        Assert.True(Validation.ContainsPathTraversal("../x"));
        Assert.True(Validation.ContainsPathTraversal("a/../../b"));
        Assert.False(Validation.ContainsPathTraversal("logs/session.log"));
        Assert.False(Validation.ContainsPathTraversal((string?)null));
    }
}
