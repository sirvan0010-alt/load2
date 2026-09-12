using Xunit;

namespace MailLoadTester.Tests;

public sealed class MultiAccountRunnerTests
{
    static MailTestOptions Base(params SmtpAccount[] accounts) => new(
        From: "a@test.local",
        Recipients: new[] { "b@test.local" },
        SmtpHost: "127.0.0.1",
        Port: 25,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 3,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 0,
        MaxConcurrency: 2,
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
        Unauthorized: true,
        Accounts: accounts.Length == 0 ? null : accounts);

    [Fact]
    public async Task DryRun_SingleAccountPath_StillWorks_WithNullAccounts()
    {
        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(Base(), new Progress<ProgressUpdate>(), CancellationToken.None);
        Assert.Equal(3, result.Sent);
        Assert.False(string.IsNullOrEmpty(result.RunId));
    }

    [Fact]
    public async Task DryRun_WithOneAccount_UsesSinglePathSemantics()
    {
        var accounts = new[]
        {
            new SmtpAccount("only", "127.0.0.1", 25, SmtpSecurity.None, false, "", "")
        };
        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(Base(accounts), new Progress<ProgressUpdate>(), CancellationToken.None);
        Assert.Equal(3, result.Sent);
    }

    [Fact]
    public async Task DryRun_WithTwoAccounts_CompletesWithoutSmtp()
    {
        var accounts = new[]
        {
            new SmtpAccount("a", "127.0.0.1", 25, SmtpSecurity.None, false, "", ""),
            new SmtpAccount("b", "127.0.0.1", 26, SmtpSecurity.None, false, "", "")
        };
        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(Base(accounts), new Progress<ProgressUpdate>(), CancellationToken.None);
        Assert.Equal(3, result.Sent);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public void Validate_RejectsAccountWithoutHost()
    {
        var bad = new SmtpAccount("x", "", 25, SmtpSecurity.None, false, "", "");
        var o = Base(bad, new SmtpAccount("y", "h", 25, SmtpSecurity.None, false, "", ""));
        Assert.Throws<ArgumentException>(() => Validation.Validate(o));
    }
}
