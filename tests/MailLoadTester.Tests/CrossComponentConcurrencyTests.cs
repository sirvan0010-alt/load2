using System.Diagnostics;
using Xunit;

namespace MailLoadTester.Tests;

/// <summary>
/// CONC-AUDIT-001/002 — combined limiter + pacing + cancel composition
/// mirroring SmtpTestRunner order: Adaptive → AcquireSendSlot → (send) → release.
/// </summary>
public sealed class CrossComponentConcurrencyTests
{
    private static MailTestOptions PaceOptions(int intervalMs) => new(
        From: "a@test.local",
        Recipients: new[] { "b@test.local" },
        SmtpHost: "127.0.0.1",
        Port: 25,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 50,
        IntervalMs: intervalMs,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 0,
        MaxConcurrency: 4,
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
        EnableBurstMode: false,
        EnableProgressiveBackoff: false,
        DetectGreylist: false,
        EnableWarmup: false,
        EnablePerRecipientLimit: false,
        EnableSendingTimeWindow: false);

    [Fact]
    public async Task AdaptiveAndRateLimiter_ParallelCancel_NoHangOrLeak()
    {
        var adaptive = new AdaptiveConcurrencyLimiter(3, 1, 3);
        var rate = new RateLimiter(intervalMs: 5);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        var tasks = Enumerable.Range(0, 24).Select(async _ =>
        {
            try
            {
                await adaptive.AcquireAsync(cts.Token);
                try
                {
                    await rate.WaitAsync(cts.Token);
                    await Task.Delay(5, cts.Token);
                }
                finally
                {
                    adaptive.Release();
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.WhenAll(tasks);
        for (var i = 0; i < 50 && adaptive.Active != 0; i++)
            await Task.Delay(10);
        Assert.Equal(0, adaptive.Active);
    }

    [Fact]
    public async Task ProxyRotator_AfterBan_ConcurrentSelectNeverReturnsBlocked()
    {
        var list = ProxyClientFactory.ParseList(
            "socks5://127.0.0.1:1080;socks5://127.0.0.1:1081;socks5://127.0.0.1:1082;socks5://127.0.0.1:1083");
        var rot = new ProxyRotator(list, random: true, blockDuration: TimeSpan.FromMinutes(5));

        var victim = rot.TryGetNext();
        Assert.NotNull(victim);
        rot.ReportBlocked(victim!);

        var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 25; i++)
            {
                var ep = rot.TryGetNext();
                if (ep is null)
                    continue;
                Assert.NotEqual(victim.DisplayKey, ep.DisplayKey);
            }
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task AdaptiveAndSendPace_ParallelCancel_NoPermitOrGateLeak()
    {
        // Models runner critical section:
        //   Adaptive.Acquire → AcquireSendSlotAsync → simulated SEND → lease dispose → Adaptive.Release
        // Cancel mid-flight must not leave adaptive.Active > 0 or hang on the send gate.
        const int maxConcurrency = 4;
        var adaptive = new AdaptiveConcurrencyLimiter(maxConcurrency, 1, maxConcurrency);
        var pace = new SmartPaceController(PaceOptions(intervalMs: 15));
        var maxObserved = 0;
        var completedSends = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var tasks = Enumerable.Range(0, 40).Select(async _ =>
        {
            try
            {
                await adaptive.AcquireAsync(cts.Token);
                try
                {
                    var now = adaptive.Active;
                    int snap;
                    do { snap = maxObserved; }
                    while (now > snap && Interlocked.CompareExchange(ref maxObserved, now, snap) != snap);

                    await using var lease = await pace.AcquireSendSlotAsync(cts.Token);
                    await Task.Delay(2, cts.Token);
                    Interlocked.Increment(ref completedSends);
                }
                finally
                {
                    adaptive.Release();
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        await Task.WhenAll(tasks);

        for (var i = 0; i < 80 && adaptive.Active != 0; i++)
            await Task.Delay(10);
        Assert.Equal(0, adaptive.Active);
        Assert.InRange(maxObserved, 1, maxConcurrency);

        // After cancel drain, a new lease must still be acquirable (send gate not stuck).
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var recovery = await pace.AcquireSendSlotAsync(cts2.Token);
        Assert.True(completedSends >= 0);
    }

    [Fact]
    public async Task AdaptiveAndSendPace_NoCancel_NeverExceedsConcurrency()
    {
        const int maxConcurrency = 3;
        var adaptive = new AdaptiveConcurrencyLimiter(maxConcurrency, 1, maxConcurrency);
        var pace = new SmartPaceController(PaceOptions(intervalMs: 5));
        var active = 0;
        var peak = 0;

        var tasks = Enumerable.Range(0, 18).Select(async _ =>
        {
            await adaptive.AcquireAsync(CancellationToken.None);
            try
            {
                var n = Interlocked.Increment(ref active);
                int p;
                do { p = peak; }
                while (n > p && Interlocked.CompareExchange(ref peak, n, p) != p);

                await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
                await Task.Delay(5);
            }
            finally
            {
                Interlocked.Decrement(ref active);
                adaptive.Release();
            }
        });

        await Task.WhenAll(tasks);
        Assert.Equal(0, adaptive.Active);
        Assert.Equal(0, active);
        Assert.InRange(peak, 1, maxConcurrency);
    }
}
