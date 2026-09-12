using Xunit;

namespace MailLoadTester.Tests;

public sealed class ProviderPresetsTests
{
    static MailTestOptions Bare() => new(
        From: "a@t.l", Recipients: new[] { "b@t.l" }, SmtpHost: "smtp.example", Port: 25,
        Security: SmtpSecurity.None, UseAuthentication: false, Username: "", Password: "",
        MessageCount: 1, IntervalMs: 9999, BatchMode: false, BatchSize: 1, BatchPauseSeconds: 0,
        MaxConcurrency: 99, Subject: "s", Body: "b", DisplayName: "n", RandomTestData: false,
        TestMode: true, AllowedDomains: "", HtmlBody: false, Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(), IgnoreCertificateErrors: false,
        MaxRetries: 0, DryRun: true, Unauthorized: true);

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        var p = ProviderPresets.Find("gmail");
        Assert.NotNull(p);
        Assert.Equal("Gmail", p!.Id);
    }

    [Fact]
    public void ApplyTo_SetsTempoAndDoesNotTouchCredentials()
    {
        var o = Bare() with { Username = "keep-user", Password = "keep-secret", SmtpHost = "keep-host" };
        var applied = ProviderPresets.ApplyTo(o, "Gmail");
        Assert.Equal("keep-user", applied.Username);
        Assert.Equal("keep-secret", applied.Password);
        Assert.Equal("keep-host", applied.SmtpHost);
        Assert.Equal("Gmail", applied.ProviderPreset);
        Assert.Equal(587, applied.Port);
        Assert.Equal(SmtpSecurity.StartTls, applied.Security);
        Assert.True(applied.IntervalMs >= 1000);
        Assert.True(applied.MaxConcurrency <= 5);
    }

    [Fact]
    public void ApplyTo_UnknownId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProviderPresets.ApplyTo(Bare(), "DoesNotExist"));
    }

    [Fact]
    public void All_Ids_AreUnique()
    {
        var ids = ProviderPresets.All.Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
