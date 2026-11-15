using MailLoadTester;
using System.Text.Json;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class MailboxMonitorTests
{
    [Fact]
    public void GetUsagePercent_ConvertsQuotaKilobytesToPercentage()
    {
        Assert.Equal(50d, MailboxMonitorMath.GetUsagePercentFromKiB(512, 1024));
    }

    [Fact]
    public void GetUsagePercent_HandlesEmptyAndFullQuota()
    {
        Assert.Equal(0d, MailboxMonitorMath.GetUsagePercentFromKiB(0, 1024));
        Assert.Equal(100d, MailboxMonitorMath.GetUsagePercentFromKiB(1024, 1024));
    }

    [Fact]
    public void GetUsagePercent_DoesNotHideUsageAboveQuota()
    {
        Assert.Equal(125d, MailboxMonitorMath.GetUsagePercentFromKiB(125, 100));
    }

    [Theory]
    [InlineData(null, 1024u)]
    [InlineData(512u, null)]
    [InlineData(512u, 0u)]
    public void GetUsagePercent_ReturnsNullWhenQuotaIsUnavailable(uint? usedKiB, uint? limitKiB)
    {
        Assert.Null(MailboxMonitorMath.GetUsagePercentFromKiB(usedKiB, limitKiB));
    }

    [Fact]
    public void ToBytes_ConvertsKiBToBytesWithoutOverflow()
    {
        Assert.Equal(1_048_576L, MailboxMonitorMath.ToBytes(1024));
    }

    [Fact]
    public void ToBytes_ReturnsNullWhenQuotaValueIsMissing()
    {
        Assert.Null(MailboxMonitorMath.ToBytes((uint?)null));
    }

    [Theory]
    [InlineData(80d, 80, true)]
    [InlineData(79.99d, 80, false)]
    [InlineData(100d, 100, true)]
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
    [Fact]
    public void Options_RedactPasswordFromToStringAndJsonSerialization()
    {
        var options = new MailboxMonitorOptions("imap.example.com", 993, MailboxMonitorSecurity.ImplicitTls, "user@example.com", "secret-value");
        Assert.DoesNotContain("secret-value", options.ToString());
        Assert.DoesNotContain("secret-value", JsonSerializer.Serialize(options));
    }

    [Fact]
    public void ValidateOptions_RejectsUnsupportedSecurityMode()
    {
        var options = new MailboxMonitorOptions("imap.example.com", 993, (MailboxMonitorSecurity)99, "user@example.com", "app-password");
        Assert.Throws<ArgumentException>(() => MailboxMonitorService.ValidateOptions(options));
    }

    [Fact]
    public void ValidateOptions_RejectsMissingHost()
    {
        var options = new MailboxMonitorOptions(" ", 993, MailboxMonitorSecurity.ImplicitTls, "user@example.com", "app-password");
        Assert.Throws<ArgumentException>(() => MailboxMonitorService.ValidateOptions(options));
    }

    [Fact]
    public void ValidateOptions_RejectsInvalidPort()
    {
        var options = new MailboxMonitorOptions("imap.example.com", 70000, MailboxMonitorSecurity.ImplicitTls, "user@example.com", "app-password");
        Assert.Throws<ArgumentOutOfRangeException>(() => MailboxMonitorService.ValidateOptions(options));
    }

    [Fact]
    public void ValidateOptions_RejectsMissingCredentials()
    {
        var options = new MailboxMonitorOptions("imap.example.com", 993, MailboxMonitorSecurity.ImplicitTls, "", "");
        Assert.Throws<ArgumentException>(() => MailboxMonitorService.ValidateOptions(options));
    }
}
