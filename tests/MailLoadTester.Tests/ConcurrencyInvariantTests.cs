using Xunit;

namespace MailLoadTester.Tests;

/// <summary>
/// A6: structural invariants — no live SMTP. Documents that MaxConcurrency is the hard upper bound
/// for adaptive admissions and validation range; worker/pool overshoot would require runner change.
/// </summary>
public class ConcurrencyInvariantTests
{
    [Fact]
    public void Adaptive_current_never_exceeds_max()
    {
        var lim = new AdaptiveConcurrencyLimiter(initial: 4, min: 1, max: 4);
        Assert.Equal(4, lim.Current);
        // Force window with only successes — should climb but not past max
        for (var i = 0; i < 20; i++)
            lim.RecordAttempt(success: true);
        Assert.True(lim.Current <= 4);
    }

    [Fact]
    public void Adaptive_errors_shrink_but_floor_at_min()
    {
        var lim = new AdaptiveConcurrencyLimiter(initial: 4, min: 1, max: 4);
        for (var i = 0; i < 20; i++)
            lim.RecordAttempt(success: false);
        Assert.True(lim.Current >= 1);
        Assert.True(lim.Current <= 4);
    }

    [Fact]
    public void Validation_MaxConcurrency_range_1_to_20()
    {
        var o = ValidDryRun(maxConcurrency: 20);
        Validation.Validate(o); // must not throw

        Assert.Throws<ArgumentException>(() => Validation.Validate(ValidDryRun(maxConcurrency: 0)));
        Assert.Throws<ArgumentException>(() => Validation.Validate(ValidDryRun(maxConcurrency: 21)));
    }

    static MailTestOptions ValidDryRun(int maxConcurrency) => new(
        From: "a@example.com",
        Recipients: new[] { "b@example.com" },
        SmtpHost: "smtp.example.com",
        Port: 587,
        Security: SmtpSecurity.StartTls,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 1,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 1,
        MaxConcurrency: maxConcurrency,
        Subject: "t",
        Body: "b",
        DisplayName: "t",
        RandomTestData: false,
        TestMode: true,
        AllowedDomains: "example.com",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: true,
        MaxRetries: 0,
        DryRun: true);
}
