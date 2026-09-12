using Xunit;

namespace MailLoadTester.Tests;

/// <summary>
/// CONC-AUDIT-001/002 — combined limiter + rotator behavior under parallel cancel.
/// </summary>
public sealed class CrossComponentConcurrencyTests
{
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
                // expected for late workers
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
        // Correct concurrency contract: once ReportBlocked has completed, no concurrent
        // TryGetNext may return that endpoint. (Selecting then later banning is not a bug.)
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
}
