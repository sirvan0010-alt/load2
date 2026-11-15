using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class ObservedResponseCapacityTests
{
    [Fact]
    public void MailboxFull_ProvidesCapacitySpecificGuidance()
    {
        var collector = new ObservedResponseCollector();
        collector.Record(552, "552 5.2.2 mailbox full");
        var row = Assert.Single(collector.Snapshot());
        Assert.Equal(ResponseClassification.MailboxFull, row.Classification);
        Assert.Contains("IMAP kvótu", row.RecommendedAction);
    }

    [Fact]
    public void MessageTooLarge_ProvidesSizeSpecificGuidance()
    {
        var collector = new ObservedResponseCollector();
        collector.Record(552, "552 5.3.4 message size exceeds fixed maximum");
        var row = Assert.Single(collector.Snapshot());
        Assert.Equal(ResponseClassification.MessageTooLarge, row.Classification);
        Assert.Contains("limit jedné zprávy", row.RecommendedAction);
    }

    [Fact]
    public void PolicyRefusal_IsNotRecommendedForAutomaticRetry()
    {
        var collector = new ObservedResponseCollector();
        collector.Record(550, "550 5.7.1 rejected by policy");
        var row = Assert.Single(collector.Snapshot());
        Assert.Equal(ResponseClassification.PolicyRejected, row.Classification);
        Assert.Contains("neopakovat naslepo", row.RecommendedAction);
    }

    [Fact]
    public void Greylist_RemainsSeparateFromRateLimit()
    {
        Assert.Equal(ResponseClassification.Greylist,
            ObservedResponseCollector.Classify(451, "451 4.7.1 temporarily deferred by greylist"));
        Assert.Equal(ResponseClassification.RateLimit,
            ObservedResponseCollector.Classify(421, "421 4.7.0 rate limit exceeded"));
    }
}
