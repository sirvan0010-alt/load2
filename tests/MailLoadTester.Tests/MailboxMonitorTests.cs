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
    public void CalculateRunDelta_ReportsOnlyMetricsAvailableInBothSnapshots()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-2);
        var end = DateTimeOffset.UtcNow;
        var before = new MailboxMonitorSnapshot(
            InboxMessageCount: 100,
            InboxUnreadCount: 20,
            InboxSizeBytes: 1_000_000,
            QuotaUsedBytes: 2_000_000,
            QuotaLimitBytes: 10_000_000,
            QuotaMessageCount: 120,
            QuotaMessageLimit: 1000,
            CheckedAt: start,
            Status: "quota available",
            Folders: [new MailboxFolderSnapshot("Junk", "Junk", false, true, false, false, 5, 1, 50_000)]);
        var after = new MailboxMonitorSnapshot(
            InboxMessageCount: 108,
            InboxUnreadCount: 22,
            InboxSizeBytes: 1_080_000,
            QuotaUsedBytes: 2_120_000,
            QuotaLimitBytes: 10_000_000,
            QuotaMessageCount: 129,
            QuotaMessageLimit: 1000,
            CheckedAt: end,
            Status: "quota available",
            Folders: [new MailboxFolderSnapshot("Spam", "Spam", false, true, false, false, 7, 2, 70_000)]);

        var delta = MailboxMonitorMath.CalculateRunDelta(before, after);

        Assert.NotNull(delta);
        Assert.Equal(9L, delta.QuotaMessageCountDelta);
        Assert.Equal(120_000L, delta.QuotaStorageBytesDelta);
        Assert.Equal(8L, delta.InboxMessageCountDelta);
        Assert.Equal(2L, delta.JunkMessageCountDelta);
        Assert.Contains("nelze", delta.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalculateRunDelta_LeavesUnsupportedMetricsUnknown()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        var end = DateTimeOffset.UtcNow;
        var before = new MailboxMonitorSnapshot(10, 2, null, null, null, null, null, start, "unsupported");
        var after = new MailboxMonitorSnapshot(12, 3, null, null, null, null, null, end, "unsupported");

        var delta = MailboxMonitorMath.CalculateRunDelta(before, after);

        Assert.NotNull(delta);
        Assert.Null(delta.QuotaMessageCountDelta);
        Assert.Null(delta.QuotaStorageBytesDelta);
        Assert.Equal(2L, delta.InboxMessageCountDelta);
        Assert.Null(delta.JunkMessageCountDelta);
    }

    [Fact]
    public void CalculateRunDelta_RequiresTwoOrderedSnapshots()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var snapshot = new MailboxMonitorSnapshot(1, 0, null, null, null, null, null, timestamp, "unknown");

        Assert.Null(MailboxMonitorMath.CalculateRunDelta(null, snapshot));
        Assert.Null(MailboxMonitorMath.CalculateRunDelta(snapshot, snapshot));
    }

    [Fact]
    public void MailboxFeedback_SlowsAfterRepeatedGrowthNearWarningThreshold()
    {
        var controller = new MailboxCapacityFeedbackController();
        var t0 = DateTimeOffset.UtcNow;
        controller.SetBaseline(QuotaSnapshot(7_000, 10_000, t0));

        Assert.Null(controller.Observe(QuotaSnapshot(7_100, 10_000, t0.AddSeconds(30)), 80));
        var moderate = controller.Observe(QuotaSnapshot(7_200, 10_000, t0.AddSeconds(60)), 80);

        Assert.NotNull(moderate);
        Assert.Equal(1, moderate.Severity);
        Assert.Equal(72d, moderate.QuotaUsagePercent);
        Assert.Equal(100L, moderate.StorageGrowthBytes);

        var critical = controller.Observe(QuotaSnapshot(7_600, 10_000, t0.AddSeconds(90)), 80);
        Assert.NotNull(critical);
        Assert.Equal(2, critical.Severity);
        Assert.Equal(76d, critical.QuotaUsagePercent);
    }

    [Fact]
    public void MailboxFeedback_UnknownQuotaOrNonGrowthDoesNotTriggerPressure()
    {
        var controller = new MailboxCapacityFeedbackController();
        var t0 = DateTimeOffset.UtcNow;
        controller.SetBaseline(new MailboxMonitorSnapshot(1, 0, null, null, null, null, null, t0, "quota unavailable"));
        Assert.Null(controller.Observe(QuotaSnapshot(7_100, 10_000, t0.AddSeconds(30)), 80));

        controller.SetBaseline(QuotaSnapshot(7_000, 10_000, t0));
        Assert.Null(controller.Observe(QuotaSnapshot(7_100, 10_000, t0.AddSeconds(30)), 80));
        Assert.Null(controller.Observe(QuotaSnapshot(7_100, 10_000, t0.AddSeconds(60)), 80));
        Assert.Null(controller.Observe(QuotaSnapshot(7_200, 10_000, t0.AddSeconds(90)), 80));
    }

    [Fact]
    public void MailboxFeedback_OnlyEscalatesAndNeverRecommendsSpeedIncrease()
    {
        var controller = new MailboxCapacityFeedbackController();
        var t0 = DateTimeOffset.UtcNow;
        controller.SetBaseline(QuotaSnapshot(7_000, 10_000, t0));

        var moderate = controller.Observe(QuotaSnapshot(7_100, 10_000, t0.AddSeconds(30)), 80);
        Assert.Null(moderate);
        moderate = controller.Observe(QuotaSnapshot(7_200, 10_000, t0.AddSeconds(60)), 80);
        Assert.Equal(1, moderate!.Severity);

        var noDowngrade = controller.Observe(QuotaSnapshot(7_300, 10_000, t0.AddSeconds(90)), 80);
        Assert.Null(noDowngrade);
    }

    private static MailboxMonitorSnapshot QuotaSnapshot(long usedBytes, long limitBytes, DateTimeOffset checkedAt)
        => new(0, 0, 0, usedBytes, limitBytes, null, null, checkedAt, "quota available");

    [Fact]
    public void Snapshot_AggregatesJunkFoldersWithoutInventingMissingCounts()
    {
        var snapshot = new MailboxMonitorSnapshot(
            InboxMessageCount: 12,
            InboxUnreadCount: 3,
            InboxSizeBytes: 4096,
            QuotaUsedBytes: null,
            QuotaLimitBytes: null,
            QuotaMessageCount: null,
            QuotaMessageLimit: null,
            CheckedAt: DateTimeOffset.UtcNow,
            Status: "quota unavailable",
            Folders:
            [
                new MailboxFolderSnapshot("Junk", "Junk", false, true, false, false, 4, 2, 2048),
                new MailboxFolderSnapshot("Spam", "Spam", false, true, false, false, null, null, null),
                new MailboxFolderSnapshot("INBOX", "INBOX", true, false, false, false, 12, 3, 4096)
            ]);

        Assert.Equal(4, snapshot.JunkMessageCount);
        Assert.Equal(2048L, snapshot.JunkSizeBytes);
    }

    [Fact]
    public void Snapshot_LeavesJunkMetricsUnknownWhenNoJunkFolderIsReadable()
    {
        var snapshot = new MailboxMonitorSnapshot(
            12, 3, 4096, null, null, null, null, DateTimeOffset.UtcNow, "quota unavailable",
            [new MailboxFolderSnapshot("Spam", "Spam", false, true, false, false, null, null, null)]);

        Assert.Null(snapshot.JunkMessageCount);
        Assert.Null(snapshot.JunkSizeBytes);
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

    [Fact]
    public void ValidateOptions_RejectsWhitespaceOnlyPassword()
    {
        var options = new MailboxMonitorOptions("imap.example.com", 993, MailboxMonitorSecurity.ImplicitTls, "user@example.com", "   ");
        Assert.Throws<ArgumentException>(() => MailboxMonitorService.ValidateOptions(options));
    }
}
