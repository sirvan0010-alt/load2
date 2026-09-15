using Xunit;

namespace MailLoadTester.Tests;

public sealed class EndpointCanonicalizerTests
{
    [Fact]
    public void Host_NormalizesDnsCaseAndTrailingDot()
    {
        Assert.Equal("smtp.example", EndpointCanonicalizer.Host(" SMTP.Example. "));
    }

    [Fact]
    public void Smtp_IncludesPortAndSecurity()
    {
        Assert.Equal("smtp.example:587/StartTls", EndpointCanonicalizer.Smtp("SMTP.EXAMPLE.", 587, SmtpSecurity.StartTls));
    }

    [Fact]
    public void Email_PreservesLocalPartAndNormalizesDomain()
    {
        Assert.Equal("User.Name@Example.COM", "User.Name@Example.COM".Trim());
        Assert.Equal("User.Name@example.com", EndpointCanonicalizer.Email(" User.Name@Example.COM. "));
    }

    [Fact]
    public void TargetSet_DeduplicatesEquivalentDomainCasing()
    {
        var targets = TargetSet.FromRecipients(new[]
        {
            "user@example.com",
            "user@EXAMPLE.COM.",
            "other@example.com"
        });

        Assert.Equal(2, targets.Count);
        Assert.Equal(new[] { "user@example.com", "other@example.com" }, targets.Recipients);
    }

    [Fact]
    public void SmtpAccount_HealthKeyUsesCanonicalEndpoint()
    {
        var account = new SmtpAccount("a", "SMTP.Example.", 587, SmtpSecurity.StartTls, false, "", "");
        Assert.Equal("a|smtp.example:587/StartTls", account.HealthKey);
    }
}
