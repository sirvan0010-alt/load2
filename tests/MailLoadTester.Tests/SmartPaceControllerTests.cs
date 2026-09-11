using System.Diagnostics;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmartPaceControllerTests
{
    private static MailTestOptions BaseOptions(int intervalMs) => new(
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
        EnableJitter: false,
        JitterPercent: 0,
        EnableBurstMode: false,
        EnableProgressiveBackoff: false,
        DetectGreylist: false,
        EnableWarmup: false,
        EnablePerRecipientLimit: false,
        EnableSendingTimeWindow: false);

    [Fact]
    public async Task ActualSendGate_FirstSendIsImmediate()
    {
        using var pace = new SmartPaceController(BaseOptions(1_000));
        var sw = Stopwatch.StartNew();

        await using (var lease = await pace.AcquireSendSlotAsync(CancellationToken.None))
        {
            sw.Stop();
        }

        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(250),
            $"First SEND gate acquisition took {sw.Elapsed.TotalMilliseconds:F0} ms; first send must not wait a full interval.");
    }

    [Fact]
    public async Task ActualSendGate_ConcurrentSendsRespectInterval()
    {
        const int intervalMs = 80;
        const int messages = 8;
        using var pace = new SmartPaceController(BaseOptions(intervalMs));
        var sendTimes = new long[messages];

        var tasks = Enumerable.Range(0, messages).Select(async i =>
        {
            await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
            sendTimes[i] = Stopwatch.GetTimestamp();
            await Task.Delay(1);
        });

        await Task.WhenAll(tasks);

        var ordered = sendTimes.OrderBy(x => x).ToArray();
        for (var i = 1; i < ordered.Length; i++)
        {
            var elapsedMs = (ordered[i] - ordered[i - 1]) * 1000.0 / Stopwatch.Frequency;
            Assert.True(elapsedMs >= intervalMs * 0.85,
                $"Actual SEND spacing was {elapsedMs:F1} ms; expected at least ~{intervalMs} ms.");
        }
    }

    [Fact]
    public async Task ActualSendGate_CancellationDoesNotLeakGate()
    {
        using var pace = new SmartPaceController(BaseOptions(0));
        await using var firstLease = await pace.AcquireSendSlotAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await pace.AcquireSendSlotAsync(cts.Token);
        });

        firstLease.Dispose();

        await using var secondLease = await pace.AcquireSendSlotAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ZeroInterval_DoesNotBlock()
    {
        using var pace = new SmartPaceController(BaseOptions(0));
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < 20; i++)
        {
            await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
        }

        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task PerRecipientLimit_DoesNotConsumeSendGateWhileBlocked()
    {
        var options = BaseOptions(1_000) with
        {
            EnablePerRecipientLimit = true,
            MaxMessagesPerRecipient = 1
        };
        using var pace = new SmartPaceController(options);

        Assert.True(pace.TryReserveRecipient("same@test.local"));
        pace.CommitRecipient("same@test.local");

        using var cts = new CancellationTokenSource(120);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pace.WaitBeforeSendAsync("same@test.local", cts.Token));

        await using var lease = await pace.AcquireSendSlotAsync(CancellationToken.None);
    }
}
