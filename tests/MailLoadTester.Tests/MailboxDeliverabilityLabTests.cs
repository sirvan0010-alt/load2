using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class MailboxDeliverabilityLabTests
{
    [Fact]
    public void Deliver_AcceptsAndReadsMessage()
    {
        var lab = new InMemoryMailboxDeliverabilityLab(new MailboxLabQuota(10, 10_000));
        var result = lab.Deliver("box@example.test", "sender.test", "confirmation", 500, SimulatedMailOutcome.Accepted, "pass", DateTimeOffset.UnixEpoch);

        Assert.Equal(MailboxDeliveryDisposition.Accepted, result.Disposition);
        Assert.Single(lab.Read("box@example.test"));
        Assert.Equal(500, lab.Snapshot("box@example.test").UsedBytes);
    }

    [Fact]
    public void Deliver_EnforcesMessageAndByteQuota()
    {
        var lab = new InMemoryMailboxDeliverabilityLab(new MailboxLabQuota(2, 700));
        lab.Deliver("box@example.test", "sender.test", "welcome", 600, SimulatedMailOutcome.Accepted, "pass");
        var result = lab.Deliver("box@example.test", "sender.test", "welcome", 500, SimulatedMailOutcome.Accepted, "pass");

        Assert.Equal(MailboxDeliveryDisposition.QuotaExceeded, result.Disposition);
        Assert.Equal(600, result.UsedBytes);
        Assert.True(lab.Snapshot("box@example.test").UnderPressure);
    }

    [Fact]
    public void Deliver_RejectsNonDeliverableProviderOutcome()
    {
        var lab = new InMemoryMailboxDeliverabilityLab();
        var result = lab.Deliver("box@example.test", "sender.test", "confirmation", 100, SimulatedMailOutcome.Throttled, "unknown");

        Assert.Equal(MailboxDeliveryDisposition.Rejected, result.Disposition);
        Assert.Empty(lab.Read("box@example.test"));
    }

    [Fact]
    public void Recover_RemovesMessagesAndReleasesQuota()
    {
        var lab = new InMemoryMailboxDeliverabilityLab(new MailboxLabQuota(10, 10_000));
        lab.Deliver("box@example.test", "sender.test", "welcome", 700, SimulatedMailOutcome.Accepted, "pass");
        lab.Deliver("box@example.test", "sender.test", "welcome", 300, SimulatedMailOutcome.Accepted, "pass");

        Assert.Equal(1, lab.Recover("box@example.test", 1));
        var snapshot = lab.Snapshot("box@example.test");
        Assert.Equal(1, snapshot.MessageCount);
        Assert.Equal(300, snapshot.UsedBytes);
    }

    [Fact]
    public void Cancellation_IsHonored()
    {
        var lab = new InMemoryMailboxDeliverabilityLab();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => lab.Deliver(
            "box@example.test", "sender.test", "welcome", 100, SimulatedMailOutcome.Accepted, "pass", cancellationToken: cts.Token));
    }

    [Fact]
    public void ConfigureMailbox_RejectsQuotaBelowCurrentUsage()
    {
        var lab = new InMemoryMailboxDeliverabilityLab(new MailboxLabQuota(10, 10_000));
        lab.Deliver("box@example.test", "sender.test", "welcome", 1000, SimulatedMailOutcome.Accepted, "pass");

        Assert.Throws<InvalidOperationException>(() => lab.ConfigureMailbox("box@example.test", new MailboxLabQuota(10, 500)));
    }
}
