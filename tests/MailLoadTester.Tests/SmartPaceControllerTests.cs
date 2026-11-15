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
    public async Task PositiveInterval_OverlapsTransfersLongerThanTheInterval()
    {
        const int intervalMs = 40;
        const int transferMs = 160;
        const int messages = 5;
        var pace = new SmartPaceController(BaseOptions(intervalMs));
        var active = 0;
        var maxActive = 0;
        var starts = new System.Collections.Concurrent.ConcurrentBag<long>();
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, messages).Select(async _ =>
        {
            await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
            starts.Add(sw.ElapsedMilliseconds);
            var nowActive = Interlocked.Increment(ref active);
            UpdateMax(ref maxActive, nowActive);
            try { await Task.Delay(transferMs); }
            finally { Interlocked.Decrement(ref active); }
        });

        await Task.WhenAll(tasks);

        Assert.True(maxActive >= 2,
            $"Expected overlapping transfers for interval={intervalMs}ms and duration={transferMs}ms; got {maxActive}.");
        var ordered = starts.OrderBy(x => x).ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            Assert.True(ordered[i] - ordered[i - 1] >= intervalMs - 20,
                $"SEND start gap was only {ordered[i] - ordered[i - 1]}ms.");
        }
    }

    [Fact]
    public async Task ZeroInterval_SendLeasesAllowConcurrentNetworkTransfers()
    {
        const int workers = 5;
        var pace = new SmartPaceController(BaseOptions(0));
        var active = 0;
        var maxActive = 0;

        var tasks = Enumerable.Range(0, workers).Select(async _ =>
        {
            await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
            var nowActive = Interlocked.Increment(ref active);
            UpdateMax(ref maxActive, nowActive);
            try
            {
                // Represents the time spent inside SMTP SendAsync. The pacing
                // reservation must not hold a global lock for this duration.
                await Task.Delay(100);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });

        await Task.WhenAll(tasks);
        Assert.True(maxActive >= 2,
            $"Expected overlapping SMTP transfers, observed max concurrency {maxActive}.");
    }

    private static void UpdateMax(ref int target, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (value <= current || Interlocked.CompareExchange(ref target, value, current) == current)
                return;
        }
    }

    [Fact]
    public async Task LastEffectiveInterval_IncludesWarmupAndMailboxPressureFloor()
    {
        var warmup = new SmartPaceController(BaseOptions(500) with
        {
            EnableWarmup = true,
            WarmupPhases = "10;20"
        });

        await using (await warmup.AcquireSendSlotAsync(CancellationToken.None)) { }
        Assert.Equal(1_000, warmup.LastEffectiveIntervalMs);

        var mailboxPressure = new SmartPaceController(BaseOptions(0));
        mailboxPressure.ApplyMailboxPressure(1);
        await using (await mailboxPressure.AcquireSendSlotAsync(CancellationToken.None)) { }
        Assert.Equal(1_000, mailboxPressure.LastEffectiveIntervalMs);
    }

    [Fact]
    public async Task MailboxPressureFloor_CannotBeUndercutByJitter()
    {
        var pace = new SmartPaceController(BaseOptions(500, jitter: true) with { JitterPercent = 90 });
        pace.ApplyMailboxPressure(1);

        var sw = Stopwatch.StartNew();
        await using (await pace.AcquireSendSlotAsync(CancellationToken.None)) { }
        await using (await pace.AcquireSendSlotAsync(CancellationToken.None)) { }
        sw.Stop();

        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(850),
            $"Jitter shortened a mailbox-pressure floor: {sw.Elapsed.TotalMilliseconds:F0} ms.");
    }

    [Fact]
    public void MailboxPressure_OnlyIncreasesGlobalInterval()
    {
        var pace = new SmartPaceController(BaseOptions(250));

        pace.ApplyMailboxPressure(1);
        var moderateInterval = pace.CurrentIntervalMs;
        pace.ApplyMailboxPressure(2);
        var criticalInterval = pace.CurrentIntervalMs;

        Assert.True(moderateInterval >= 250);
        Assert.True(criticalInterval >= moderateInterval);
        Assert.True(criticalInterval >= 2_000);
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

    [Fact]
    public async Task FirstSend_IsImmediate_SecondRespectsInterval()
    {
        const int intervalMs = 80;
        var pace = new SmartPaceController(BaseOptions(intervalMs));
        var sw = Stopwatch.StartNew();

        await using (await pace.AcquireSendSlotAsync(CancellationToken.None)) { }
        var firstMs = sw.ElapsedMilliseconds;
        Assert.True(firstMs < 40, $"First SEND should be immediate, took {firstMs} ms");

        sw.Restart();
        await using (await pace.AcquireSendSlotAsync(CancellationToken.None)) { }
        var secondMs = sw.ElapsedMilliseconds;
        Assert.True(secondMs >= intervalMs - 25,
            $"Second SEND should wait ~{intervalMs} ms, took {secondMs} ms");
    }

    [Fact]
    public async Task ActualSendGate_CancellationDoesNotLeakGate()
    {
        var pace = new SmartPaceController(BaseOptions(intervalMs: 200));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var lease = await pace.AcquireSendSlotAsync(cts.Token);
            await Task.Delay(500, cts.Token);
        });

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var lease2 = await pace.AcquireSendSlotAsync(cts2.Token);
    }

    [Fact]
    public async Task ActualSendGate_ConcurrentSendsRespectInterval()
    {
        const int intervalMs = 80;
        var pace = new SmartPaceController(BaseOptions(intervalMs));
        var starts = new long[4];
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, 4).Select(async i =>
        {
            await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
            starts[i] = sw.ElapsedMilliseconds;
            await Task.Delay(5);
        });
        await Task.WhenAll(tasks);
        Array.Sort(starts);

        for (var i = 1; i < starts.Length; i++)
        {
            Assert.True(starts[i] - starts[i - 1] >= intervalMs - 35,
                $"Gap {i} was only {starts[i] - starts[i - 1]} ms (starts={string.Join(',', starts)})");
        }
    }
}
