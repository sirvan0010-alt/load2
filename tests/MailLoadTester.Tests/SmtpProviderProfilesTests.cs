using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmtpProviderProfilesTests
{
    [Fact]
    public void All_Ids_AreUnique()
    {
        var ids = SmtpProviderProfiles.All.Select(p => p.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("Gmail", "smtp.gmail.com", 587, SmtpSecurity.StartTls)]
    [InlineData("Microsoft365", "smtp.office365.com", 587, SmtpSecurity.StartTls)]
    [InlineData("Outlook", "smtp-mail.outlook.com", 587, SmtpSecurity.StartTls)]
    [InlineData("Seznam", "smtp.seznam.cz", 465, SmtpSecurity.ImplicitTls)]
    [InlineData("Yahoo", "smtp.mail.yahoo.com", 465, SmtpSecurity.ImplicitTls)]
    [InlineData("iCloud", "smtp.mail.me.com", 587, SmtpSecurity.StartTls)]
    [InlineData("Zoho", "smtp.zoho.com", 587, SmtpSecurity.StartTls)]
    [InlineData("ZohoEU", "smtp.zoho.eu", 587, SmtpSecurity.StartTls)]
    [InlineData("Fastmail", "smtp.fastmail.com", 465, SmtpSecurity.ImplicitTls)]
    public void Provider_HasExpectedSubmissionEndpoint(string id, string host, int port, SmtpSecurity security)
    {
        var profile = SmtpProviderProfiles.Find(id);
        Assert.NotNull(profile);
        Assert.Equal(host, profile!.Host);
        Assert.Equal(port, profile.Port);
        Assert.Equal(security, profile.Security);
    }

    [Fact]
    public void FindByHost_IsCaseInsensitiveAndTrimsWhitespace()
    {
        Assert.Equal("Seznam", SmtpProviderProfiles.FindByHost(" SMTP.SEZNAM.CZ ")?.Id);
    }

    [Theory]
    [InlineData("user@gmail.com", "Gmail")]
    [InlineData("user@googlemail.com", "Gmail")]
    [InlineData("user@hotmail.com", "Outlook")]
    [InlineData("user@seznam.cz", "Seznam")]
    [InlineData("user@email.cz", "Seznam")]
    [InlineData("user@yahoo.co.uk", "Yahoo")]
    [InlineData("user@icloud.com", "iCloud")]
    [InlineData("user@zoho.eu", "ZohoEU")]
    [InlineData("user@fastmail.fm", "Fastmail")]
    public void FindByEmail_DetectsKnownMailboxDomains(string email, string expectedId)
    {
        Assert.Equal(expectedId, SmtpProviderProfiles.FindByEmail(email)?.Id);
    }

    [Fact]
    public void Detect_PrefersExplicitSmtpHostOverEmailDomain()
    {
        Assert.Equal("Seznam", SmtpProviderProfiles.Detect("smtp.seznam.cz", "user@gmail.com")?.Id);
    }

    [Fact]
    public void FindByEmail_UnknownOrInvalidAddressReturnsNull()
    {
        Assert.Null(SmtpProviderProfiles.FindByEmail("not-an-email"));
        Assert.Null(SmtpProviderProfiles.FindByEmail("user@custom-domain.example"));
    }

    [Fact]
    public void ApplyTo_ChangesOnlySmtpConnectionDefaults()
    {
        var options = new MailTestOptions(
            From: "sender@example.test", Recipients: new[] { "recipient@example.test" },
            SmtpHost: "old.example.test", Port: 25, Security: SmtpSecurity.None,
            UseAuthentication: false, Username: "user", Password: "secret",
            MessageCount: 1, IntervalMs: 1234, BatchMode: false, BatchSize: 1,
            BatchPauseSeconds: 0, MaxConcurrency: 1, Subject: "subject", Body: "body",
            DisplayName: "sender", RandomTestData: false, TestMode: true,
            AllowedDomains: "example.test", HtmlBody: false, Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(), IgnoreCertificateErrors: false,
            MaxRetries: 0, DryRun: true, ProxyHost: "127.0.0.1", ProxyPort: 1080);

        var applied = SmtpProviderProfiles.ApplyTo(options, SmtpProviderProfiles.Find("Seznam")!);

        Assert.Equal("smtp.seznam.cz", applied.SmtpHost);
        Assert.Equal(465, applied.Port);
        Assert.Equal(SmtpSecurity.ImplicitTls, applied.Security);
        Assert.True(applied.UseAuthentication);
        Assert.Equal(SmtpAuthMethod.Auto, applied.AuthMethod);
        Assert.Equal("user", applied.Username);
        Assert.Equal("secret", applied.Password);
        Assert.Equal(options.From, applied.From);
        Assert.Equal(options.Recipients, applied.Recipients);
        Assert.Equal(options.IntervalMs, applied.IntervalMs);
        Assert.Equal(options.ProxyHost, applied.ProxyHost);
    }

    [Theory]
    [InlineData("Microsoft365", SmtpAuthMethod.OAuth2)]
    [InlineData("Outlook", SmtpAuthMethod.OAuth2)]
    [InlineData("Fastmail", SmtpAuthMethod.Plain)]
    public void Provider_UsesDocumentedAuthenticationMechanism(string id, SmtpAuthMethod expected)
    {
        Assert.Equal(expected, SmtpProviderProfiles.Find(id)?.AuthMethod);
    }

    [Fact]
    public void Find_UnknownProvider_ReturnsNull()
    {
        Assert.Null(SmtpProviderProfiles.Find("unknown"));
    }
}
