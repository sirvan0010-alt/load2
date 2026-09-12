using MailLoadTester;
using System.Diagnostics;
using Xunit;

namespace MailLoadTester.Tests;

public class RateLimiterTests
{
    [Fact]
    public async Task ZeroInterval_ReturnsImmediately()
    {
        var limiter = new RateLimiter(0);
        var sw = Stopwatch.StartNew();

        await limiter.WaitAsync(CancellationToken.None);

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ConcurrentWaiters_AreGloballySpaced()
    {
        const int intervalMs = 40;
        var limiter = new RateLimiter(intervalMs);
        var times = new long[3];
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, 3).Select(async i =>
        {
            await limiter.WaitAsync(CancellationToken.None);
            times[i] = sw.ElapsedMilliseconds;
        }).ToArray();

        await Task.WhenAll(tasks);
        Array.Sort(times);

        Assert.True(times[1] - times[0] >= intervalMs - 15,
            $"Second start was only {times[1] - times[0]} ms after the first.");
        Assert.True(times[2] - times[1] >= intervalMs - 15,
            $"Third start was only {times[2] - times[1]} ms after the second.");
    }

    [Fact]
    public async Task CancelledLastReservation_DoesNotLeavePhantomDelay()
    {
        // Contract: cancelled reservation must not stack an *extra* full interval
        // on top of spacing from the last granted slot.
        // After grant at T0, next wait is due ~T0+interval — not T0+2*interval.
        // CI runners can be slow/fast; assert no double-interval phantom, not exact ms.
        const int intervalMs = 200;
        var limiter = new RateLimiter(intervalMs);
        using var cts = new CancellationTokenSource();

        await limiter.WaitAsync(CancellationToken.None); // grant at T0 (immediate)

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.WaitAsync(cts.Token));

        var sw = Stopwatch.StartNew();
        await limiter.WaitAsync(CancellationToken.None);
        sw.Stop();

        // Lower bound: some spacing should remain (not immediate free-for-all),
        // but CI jitter can deliver early — require only half-interval floor.
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(intervalMs / 2 - 20),
            $"Expected meaningful spacing after grant, got {sw.ElapsedMilliseconds} ms.");
        // Upper bound: must not be ~2× interval (phantom stacked cancel).
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(intervalMs * 1.75),
            $"Cancelled reservation left a stacked phantom delay: {sw.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task CancelledEarlierReservation_DoesNotMoveScheduleBackwards()
    {
        var limiter = new RateLimiter(100);
        using var first = new CancellationTokenSource();
        var firstWait = limiter.WaitAsync(first.Token);

        await Task.Delay(10);

        using var second = new CancellationTokenSource();
        var secondWait = limiter.WaitAsync(second.Token);
        second.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondWait);
        await firstWait;

        var sw = Stopwatch.StartNew();
        await limiter.WaitAsync(CancellationToken.None);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(50),
            $"Schedule was moved backwards after cancelling an earlier reservation: {sw.ElapsedMilliseconds} ms.");
    }
}
