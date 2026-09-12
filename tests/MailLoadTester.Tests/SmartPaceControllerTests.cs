using System.Diagnostics;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmartPaceControllerTests
{
    private static MailTestOptions BaseOptions(int intervalMs, bool jitter = false) => new(
        From: "a@test.local",
        Recipients: new[] { "b@test.local" },
        SmtpHost: "127.0.0.1",
        Port: 25,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 10,
        IntervalMs: intervalMs,
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
        EnableJitter: jitter,
        JitterPercent: 0,
        EnableBurstMode: false,
        EnableProgressiveBackoff: false,
        DetectGreylist: false,
        EnableWarmup: false,
        EnablePerRecipientLimit: false,
        EnableSendingTimeWindow: false);

    [Fact]
    public async Task GlobalSpacing_WithConcurrency_RespectsInterval()
    {
        const int intervalMs = 50;
        const int messages = 8;
        var pace = new SmartPaceController(BaseOptions(intervalMs));
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, messages).Select(async _ =>
        {
            await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
            pace.RecordSuccess("b@test.local");
        });
        await Task.WhenAll(tasks);
        sw.Stop();

        var minExpected = TimeSpan.FromMilliseconds((messages - 1) * intervalMs * 0.7);
        Assert.True(sw.Elapsed >= minExpected,
            $"Elapsed {sw.Elapsed.TotalMilliseconds:F0} ms < expected min {minExpected.TotalMilliseconds:F0} ms — spacing není globální?");
    }

    [Fact]
    public async Task ZeroInterval_DoesNotBlock()
    {
        var pace = new SmartPaceController(BaseOptions(0));
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
            await pace.WaitBeforeSendAsync("x@test.local", CancellationToken.None);
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task Cancellation_ReleasesWithoutHang()
    {
        var pace = new SmartPaceController(BaseOptions(5_000));
        await using (await pace.AcquireSendSlotAsync(CancellationToken.None)) { }
        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => { await using var lease = await pace.AcquireSendSlotAsync(cts.Token); });
    }

    [Fact]
    public async Task Cancellation_RemovesOnlyItsOwnReservation()
    {
        var pace = new SmartPaceController(BaseOptions(5_000));
        await using (await pace.AcquireSendSlotAsync(CancellationToken.None)) { }

        using var firstCts = new CancellationTokenSource();
        using var secondCts = new CancellationTokenSource();
        var first = pace.AcquireSendSlotAsync(firstCts.Token).AsTask();
        var second = pace.AcquireSendSlotAsync(secondCts.Token).AsTask();

        firstCts.Cancel();
        secondCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }

    [Fact]
    public async Task SuccessfulReservation_IsRemovedAfterWait()
    {
        var pace = new SmartPaceController(BaseOptions(20));
        await pace.WaitBeforeSendAsync("success@test.local", CancellationToken.None);
        Assert.Equal(0, pace.ScheduledReservationCount);
    }

    [Fact]
    public async Task PerRecipientLimit_DoesNotConsumeGlobalSlotWhileBlocked()
    {
        var opts = BaseOptions(1000) with
        {
            EnablePerRecipientLimit = true,
            MaxMessagesPerRecipient = 1,
            PerRecipientWindowMinutes = 60
        };
        var pace = new SmartPaceController(opts);
        Assert.True(pace.TryReserveRecipient("same@test.local"));
        pace.CommitRecipient("same@test.local");

        using var cts = new CancellationTokenSource(120);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pace.WaitBeforeSendAsync("same@test.local", cts.Token));

        Assert.Equal(0, pace.ScheduledReservationCount);
    }
}
