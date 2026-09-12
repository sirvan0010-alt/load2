using Xunit;

namespace MailLoadTester.Tests;

public sealed class DeliveryLedgerTests
{
    [Fact]
    public void Accepted_message_cannot_be_claimed_again()
    {
        var ledger = new MailLoadTester.DeliveryLedger(1);

        Assert.True(ledger.TryClaim(1));
        Assert.True(ledger.TryMarkAccepted(1));
        Assert.False(ledger.TryClaim(1));
        Assert.Equal(MailLoadTester.DeliveryState.Accepted, ledger.GetState(1));
        Assert.Equal(1, ledger.CountAccepted());
    }

    [Fact]
    public void Failed_message_becomes_eligible_for_restart()
    {
        var ledger = new MailLoadTester.DeliveryLedger(1);

        Assert.True(ledger.TryClaim(1));
        Assert.True(ledger.MarkFailed(1));
        Assert.True(ledger.TryClaim(1));
        Assert.True(ledger.TryMarkAccepted(1));
        Assert.False(ledger.TryClaim(1));
        Assert.Equal(1, ledger.CountAccepted());
        Assert.Equal(0, ledger.CountFailed());
    }

    [Fact]
    public void InFlight_message_is_not_claimed_by_two_workers()
    {
        var ledger = new MailLoadTester.DeliveryLedger(1);
        var results = Enumerable.Range(0, 32)
            .AsParallel()
            .Select(_ => ledger.TryClaim(1))
            .ToArray();

        Assert.Equal(1, results.Count(static x => x));
        Assert.Equal(MailLoadTester.DeliveryState.InFlight, ledger.GetState(1));
    }

    [Fact]
    public void Accepted_state_cannot_be_reset_to_failed()
    {
        var ledger = new MailLoadTester.DeliveryLedger(1);

        Assert.True(ledger.TryClaim(1));
        Assert.True(ledger.TryMarkAccepted(1));
        Assert.False(ledger.MarkFailed(1));
        Assert.Equal(MailLoadTester.DeliveryState.Accepted, ledger.GetState(1));
    }

    [Fact]
    public void InFlight_MarkFailed_AllowsReclaim_AfterCancelSemantics()
    {
        // Phase A/B 1.5 + 2.6: cancel path marks InFlight → Failed so AutoRestart can reclaim.
        var ledger = new MailLoadTester.DeliveryLedger(1);
        Assert.True(ledger.TryClaim(1));
        Assert.Equal(MailLoadTester.DeliveryState.InFlight, ledger.GetState(1));
        Assert.True(ledger.MarkFailed(1));
        Assert.Equal(MailLoadTester.DeliveryState.Failed, ledger.GetState(1));
        Assert.True(ledger.TryClaim(1));
        Assert.True(ledger.TryMarkAccepted(1));
        Assert.Equal(1, ledger.CountAccepted());
    }
}
