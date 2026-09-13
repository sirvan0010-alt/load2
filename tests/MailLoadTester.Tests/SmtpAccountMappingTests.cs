using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmtpAccountMappingTests
{
    static SmtpAccountDraft Draft(string id, bool enabled = true, string host = "smtp.example", int port = 587) =>
        new(id, host, port, SmtpSecurity.StartTls, true, id + "@ex", "secret-" + id, enabled);

    [Fact]
    public void ToOptionsAccounts_Empty_ReturnsNull()
    {
        Assert.Null(SmtpAccountMapping.ToOptionsAccounts(Array.Empty<SmtpAccountDraft>()));
        Assert.Null(SmtpAccountMapping.ToOptionsAccounts(new[] { Draft("a", enabled: false) }));
    }

    [Fact]
    public void ToOptionsAccounts_SingleEnabled()
    {
        var list = SmtpAccountMapping.ToOptionsAccounts(new[] { Draft("a") });
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal("a", list[0].Id);
    }

    [Fact]
    public void ToOptionsAccounts_Multiple_SkipsDisabled()
    {
        var list = SmtpAccountMapping.ToOptionsAccounts(new[]
        {
            Draft("a"),
            Draft("b", enabled: false),
            Draft("c")
        });
        Assert.Equal(2, list!.Count);
        Assert.DoesNotContain(list, a => a.Id == "b");
    }

    [Fact]
    public void ValidateDraft_DuplicateId_Throws()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" };
        Assert.Throws<ArgumentException>(() => SmtpAccountMapping.ValidateDraft(Draft("a"), ids));
    }

    [Fact]
    public void ValidateAll_RejectsDuplicateAmongEnabled()
    {
        Assert.Throws<ArgumentException>(() =>
            SmtpAccountMapping.ValidateAll(new[] { Draft("x"), Draft("x") }));
    }

    [Fact]
    public void ToOptionsAccounts_DoesNotAppearInRunReportSecrets()
    {
        var list = SmtpAccountMapping.ToOptionsAccounts(new[] { Draft("a") })!;
        var opts = new MailTestOptions(
            From: "a@t.l", Recipients: new[] { "b@t.l" }, SmtpHost: "h", Port: 25,
            Security: SmtpSecurity.None, UseAuthentication: true, Username: "u", Password: "PRIMARY",
            MessageCount: 1, IntervalMs: 0, BatchMode: false, BatchSize: 1, BatchPauseSeconds: 0,
            MaxConcurrency: 1, Subject: "s", Body: "b", DisplayName: "n", RandomTestData: false,
            TestMode: true, AllowedDomains: "", HtmlBody: false, Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(), IgnoreCertificateErrors: false,
            MaxRetries: 0, DryRun: true, Accounts: list);
        var report = RunReportBuilder.Create("r", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, opts,
            new MailTestResult(1, 0, 0, TimeSpan.Zero, "", 0, 0, 0, 0, 0, 0, 0));
        var json = RunReportBuilder.ToJson(report);
        Assert.DoesNotContain("secret-a", json, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIMARY", json, StringComparison.Ordinal);
    }
}
