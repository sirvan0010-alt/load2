using Xunit;

namespace MailLoadTester.Tests;

/// <summary>SEC-AUDIT-002 partial — reject path traversal segments on validated file paths.</summary>
public sealed class PathBoundaryTests
{
    static MailTestOptions Base(
        string? eml = null,
        string? sessionLog = null,
        IReadOnlyList<string>? attachments = null,
        IReadOnlyList<string>? inlineAttachments = null,
        string clientCertificatePath = "") => new(
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
        Attachments: attachments ?? Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 0,
        DryRun: true,
        EmlTemplatePath: eml,
        SessionLogPath: sessionLog ?? "",
        EnableSessionLog: !string.IsNullOrEmpty(sessionLog),
        InlineAttachments: inlineAttachments,
        ClientCertificatePath: clientCertificatePath);

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

    [Theory]
    [InlineData("../secret.bin")]
    [InlineData("assets/../../secret.bin")]
    [InlineData("..\\secret.bin")]
    public void AttachmentPath_RejectsDotDot(string path)
    {
        var o = Base(attachments: new[] { path });
        var ex = Assert.Throws<ArgumentException>(() => Validation.Validate(o));
        Assert.Contains("..", ex.Message);
    }

    [Theory]
    [InlineData("../secret.bin")]
    [InlineData("assets/../../secret.bin")]
    [InlineData("..\\secret.bin")]
    public void InlineAttachmentPath_RejectsDotDot(string path)
    {
        var o = Base(inlineAttachments: new[] { path });
        var ex = Assert.Throws<ArgumentException>(() => Validation.Validate(o));
        Assert.Contains("..", ex.Message);
    }

    [Theory]
    [InlineData("../client.pfx")]
    [InlineData("certs/../../client.pfx")]
    [InlineData("..\\client.pfx")]
    public void ClientCertificatePath_RejectsDotDot(string path)
    {
        var o = Base(clientCertificatePath: path);
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
