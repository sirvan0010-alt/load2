using Xunit;

namespace MailLoadTester.Tests;

public sealed class DirectMxRoutingTests
{
    [Fact]
    public void SingleDomain_ReturnsNormalizedDomain()
    {
        var d = DirectMxRouting.RequireSingleRecipientDomain(new[]
        {
            "alice@Example.COM.",
            "bob@example.com"
        });
        Assert.Equal("example.com", d);
    }

    [Fact]
    public void MixedDomains_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DirectMxRouting.RequireSingleRecipientDomain(new[]
            {
                "a@example.com",
                "b@other.org"
            }));
        Assert.Contains("jedné domény", ex.Message);
    }

    [Fact]
    public void MissingAt_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DirectMxRouting.RequireSingleRecipientDomain(new[] { "not-an-email" }));
    }

    [Fact]
    public void EmptyList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DirectMxRouting.RequireSingleRecipientDomain(Array.Empty<string>()));
    }

    [Fact]
    public void ExtractDomain_Normalizes()
    {
        Assert.Equal("example.com", DirectMxRouting.ExtractDomain("User@Example.COM."));
        Assert.Null(DirectMxRouting.ExtractDomain("nodomain"));
        Assert.Null(DirectMxRouting.ExtractDomain("@"));
        Assert.Null(DirectMxRouting.ExtractDomain(null));
    }

    [Fact]
    public void Validation_AlsoRejectsMixedDirectMx()
    {
        var o = new MailTestOptions(
            From: "from@example.com",
            Recipients: new[] { "a@example.com", "b@other.org" },
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
            DirectMxDelivery: true);
        var ex = Assert.Throws<ArgumentException>(() => Validation.Validate(o));
        Assert.Contains("Direct MX", ex.Message);
    }
}
