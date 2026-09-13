using Xunit;

namespace MailLoadTester.Tests;

public sealed class ProviderThrottleTests
{
    private static MailTestOptions BaseOptions() => new(
        From: "a@test.local",
        Recipients: new[] { "one@example.test" },
        SmtpHost: "127.0.0.1",
        Port: 25,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 10,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 0,
        MaxConcurrency: 5,
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
        EnableJitter: false,
        EnablePerRecipientLimit: false,
        EnableSendingTimeWindow: false,
        EnableWarmup: false,
        DetectGreylist: false,
        EnableProgressiveBackoff: false);

    [Fact]
    public async Task SameDestinationProvider_IsThrottledAcrossRecipients()
    {
        var pace = new SmartPaceController(
            BaseOptions(),
            new ProviderThrottleSettings(Enabled: true, MaxMessages: 1, WindowMinutes: 60));

        await pace.WaitBeforeSendAsync("first@example.test", CancellationToken.None);
        pace.CommitRecipient("first@example.test");
        pace.RecordSuccess("first@example.test");

        using var cts = new CancellationTokenSource(120);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pace.WaitBeforeSendAsync("second@example.test", cts.Token));
    }

    [Fact]
    public async Task DifferentDestinationProviders_HaveIndependentWindows()
    {
        var pace = new SmartPaceController(
            BaseOptions(),
            new ProviderThrottleSettings(Enabled: true, MaxMessages: 1, WindowMinutes: 60));

        await pace.WaitBeforeSendAsync("first@example.test", CancellationToken.None);
        pace.CommitRecipient("first@example.test");
        pace.RecordSuccess("first@example.test");

        await pace.WaitBeforeSendAsync("other@example.org", CancellationToken.None);
    }

    [Fact]
    public async Task FailedReservation_CanBeReleasedWithoutProviderLeak()
    {
        var pace = new SmartPaceController(
            BaseOptions(),
            new ProviderThrottleSettings(Enabled: true, MaxMessages: 1, WindowMinutes: 60));

        await pace.WaitBeforeSendAsync("first@example.test", CancellationToken.None);
        pace.ReleaseRecipient("first@example.test");

        await pace.WaitBeforeSendAsync("second@example.test", CancellationToken.None);
    }

    [Fact]
    public async Task DisabledProviderThrottle_PreservesExistingBehavior()
    {
        var pace = new SmartPaceController(
            BaseOptions(),
            new ProviderThrottleSettings(Enabled: false, MaxMessages: 1, WindowMinutes: 60));

        await pace.WaitBeforeSendAsync("first@example.test", CancellationToken.None);
        pace.CommitRecipient("first@example.test");
        pace.RecordSuccess("first@example.test");
        await pace.WaitBeforeSendAsync("second@example.test", CancellationToken.None);
    }
}
