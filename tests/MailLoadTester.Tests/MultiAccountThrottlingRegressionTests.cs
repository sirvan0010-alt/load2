using Xunit;

namespace MailLoadTester.Tests;

/// <summary>
/// Regression contract for multi-account runs: account selection must not create
/// independent destination-provider throttle state. The runner owns one
/// SmartPaceController per run, so both SMTP accounts share the same pacing state.
/// </summary>
public sealed class MultiAccountThrottlingRegressionTests
{
    private static MailTestOptions Base(params SmtpAccount[] accounts) => new(
        From: "a@test.local",
        Recipients: new[] { "one@example.test" },
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
        Accounts: accounts);

    private static SmartPaceController CreateSharedPace(params SmtpAccount[] accounts)
    {
        var options = Base(accounts);
        return new SmartPaceController(
            options,
            new ProviderThrottleSettings(Enabled: true, MaxMessages: 1, WindowMinutes: 60));
    }

    [Fact]
    public async Task TwoAccounts_SameDestinationProvider_CannotBypassSharedLimit()
    {
        var accounts = new[]
        {
            new SmtpAccount("account-a", "smtp-a.test", 587, SmtpSecurity.StartTls, true, "a", "secret-a"),
            new SmtpAccount("account-b", "smtp-b.test", 587, SmtpSecurity.StartTls, true, "b", "secret-b")
        };
        var pace = CreateSharedPace(accounts);

        await pace.WaitBeforeSendAsync("first@example.test", CancellationToken.None);
        pace.CommitRecipient("first@example.test");
        pace.RecordSuccess("first@example.test");

        using var cts = new CancellationTokenSource(120);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pace.WaitBeforeSendAsync("second@example.test", cts.Token));
    }

    [Fact]
    public async Task SameRecipient_TwoAccounts_DestinationLimitStillApplies()
    {
        var accounts = new[]
        {
            new SmtpAccount("account-a", "smtp-a.test", 587, SmtpSecurity.StartTls, true, "a", "secret-a"),
            new SmtpAccount("account-b", "smtp-b.test", 587, SmtpSecurity.StartTls, true, "b", "secret-b")
        };
        var options = Base(accounts) with
        {
            EnablePerRecipientLimit = true,
            MaxMessagesPerRecipient = 1,
            PerRecipientWindowMinutes = 60
        };
        var pace = new SmartPaceController(
            options,
            new ProviderThrottleSettings(Enabled: true, MaxMessages: 100, WindowMinutes: 60));

        await pace.WaitBeforeSendAsync("same@example.test", CancellationToken.None);
        pace.CommitRecipient("same@example.test");
        pace.RecordSuccess("same@example.test");

        using var cts = new CancellationTokenSource(120);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pace.WaitBeforeSendAsync("same@example.test", cts.Token));
    }

    [Fact]
    public async Task DifferentDestinationProviders_HaveIndependentWindows_WithTwoAccounts()
    {
        var accounts = new[]
        {
            new SmtpAccount("account-a", "smtp-a.test", 587, SmtpSecurity.StartTls, true, "a", "secret-a"),
            new SmtpAccount("account-b", "smtp-b.test", 587, SmtpSecurity.StartTls, true, "b", "secret-b")
        };
        var pace = CreateSharedPace(accounts);

        await pace.WaitBeforeSendAsync("one@example.test", CancellationToken.None);
        pace.CommitRecipient("one@example.test");
        pace.RecordSuccess("one@example.test");

        await pace.WaitBeforeSendAsync("two@example.org", CancellationToken.None);
    }

    [Fact]
    public async Task Cancellation_AfterProviderThrottleWait_DoesNotLeakReservation()
    {
        var accounts = new[]
        {
            new SmtpAccount("account-a", "smtp-a.test", 587, SmtpSecurity.StartTls, true, "a", "secret-a"),
            new SmtpAccount("account-b", "smtp-b.test", 587, SmtpSecurity.StartTls, true, "b", "secret-b")
        };
        var pace = CreateSharedPace(accounts);

        await pace.WaitBeforeSendAsync("first@example.test", CancellationToken.None);
        pace.CommitRecipient("first@example.test");
        pace.RecordSuccess("first@example.test");

        using var cts = new CancellationTokenSource(80);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pace.WaitBeforeSendAsync("second@example.test", cts.Token));

        pace.ReleaseRecipient("second@example.test");
        Assert.Equal(0, pace.ScheduledReservationCount);
    }

    [Fact]
    public async Task DryRun_MultiAccount_RemainsNetworkFree()
    {
        var accounts = new[]
        {
            new SmtpAccount("account-a", "127.0.0.1", 1, SmtpSecurity.None, false, "", ""),
            new SmtpAccount("account-b", "127.0.0.1", 2, SmtpSecurity.None, false, "", "")
        };
        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(Base(accounts), new Progress<ProgressUpdate>(), CancellationToken.None);

        Assert.Equal(3, result.Sent);
        Assert.Equal(0, result.Failed);
    }
}
