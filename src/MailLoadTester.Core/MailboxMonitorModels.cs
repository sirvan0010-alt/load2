using System.Text.Json.Serialization;

namespace MailLoadTester;

public enum MailboxMonitorSecurity
{
    ImplicitTls,
    StartTls
}

/// <summary>In-memory IMAP connection settings. Passwords are intentionally not serializable.</summary>
public sealed record MailboxMonitorOptions(
    string Host,
    int Port,
    MailboxMonitorSecurity Security,
    string Username,
    [property: JsonIgnore] string Password)
{
    public override string ToString()
        => $"MailboxMonitorOptions {{ Host = {Host}, Port = {Port}, Security = {Security}, Username = {Username}, Password = [REDACTED] }}";
}

/// <summary>Credential-free, read-only mailbox snapshot.</summary>
public sealed record MailboxMonitorSnapshot(
    int? InboxMessageCount,
    int? InboxUnreadCount,
    long? InboxSizeBytes,
    long? QuotaUsedBytes,
    long? QuotaLimitBytes,
    uint? QuotaMessageCount,
    uint? QuotaMessageLimit,
    DateTimeOffset CheckedAt,
    string Status)
{
    public double? QuotaUsagePercent => MailboxMonitorMath.GetUsagePercent(QuotaUsedBytes, QuotaLimitBytes);

    public bool HasStorageQuota => QuotaUsedBytes.HasValue && QuotaLimitBytes is > 0;
}

public static class MailboxMonitorMath
{
    public static double? GetUsagePercentFromKiB(uint? usedKiB, uint? limitKiB)
    {
        if (!usedKiB.HasValue || !limitKiB.HasValue || limitKiB.Value == 0)
            return null;

        return usedKiB.Value * 100d / limitKiB.Value;
    }

    public static double? GetUsagePercent(long? usedBytes, long? limitBytes)
    {
        if (!usedBytes.HasValue || !limitBytes.HasValue || limitBytes.Value <= 0)
            return null;

        return usedBytes.Value * 100d / limitBytes.Value;
    }

    public static long ToBytes(uint kib)
        => checked((long)kib * 1024L);

    public static long? ToBytes(uint? kib)
        => kib.HasValue ? ToBytes(kib.Value) : null;

    public static bool ShouldStop(double? usagePercent, int thresholdPercent)
    {
        if (thresholdPercent is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(thresholdPercent), "Threshold must be between 1 and 100 percent.");

        return usagePercent.HasValue && usagePercent.Value >= thresholdPercent;
    }
}