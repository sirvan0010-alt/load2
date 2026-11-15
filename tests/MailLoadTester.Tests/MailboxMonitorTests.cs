using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class MailboxMonitorTests
{
    [Fact]
    public void GetUsagePercent_ConvertsQuotaKilobytesToPercentage()
    {
        Assert.Equal(50d, MailboxMonitorMath.GetUsagePercent(512, 1024));
    }

    [Fact]
    public void GetUsagePercent_DoesNotHideUsageAboveQuota()
    {
        Assert.Equal(125d, MailboxMonitorMath.GetUsagePercent(125, 100));
    }

    [Theory]
    [InlineData(null, 1024u)]
    [InlineData(512u, null)]
    [InlineData(512u, 0u)]
    public void GetUsagePercent_ReturnsNullWhenQuotaIsUnavailable(uint? usedKiB, uint? limitKiB)
    {
        Assert.Null(MailboxMonitorMath.GetUsagePercent(usedKiB, limitKiB));
    }

    [Fact]
    public void ToBytes_ConvertsKiBToBytesWithoutOverflow()
    {
        Assert.Equal(1_048_576L, MailboxMonitorMath.ToBytes(1024));
    }

    [Theory]
    [InlineData(80d, 80, true)]
    [InlineData(79.99d, 80, false)]
    [InlineData(125d, 80, true)]
    [InlineData(null, 80, false)]
    public void ShouldStop_UsesOnlyRealQuotaPercentage(double? usagePercent, int threshold, bool expected)
    {
        Assert.Equal(expected, MailboxMonitorMath.ShouldStop(usagePercent, threshold));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ShouldStop_RejectsInvalidThreshold(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MailboxMonitorMath.ShouldStop(80, threshold));
    }
}