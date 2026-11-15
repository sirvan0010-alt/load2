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
    string Status,
    IReadOnlyList<MailboxFolderSnapshot>? Folders = null)
{
    public double? QuotaUsagePercent => MailboxMonitorMath.GetUsagePercent(QuotaUsedBytes, QuotaLimitBytes);
    public bool HasStorageQuota => QuotaUsedBytes.HasValue && QuotaLimitBytes is > 0;

    public int? JunkMessageCount
    {
        get
        {
            var junk = Folders?.Where(f => f.IsJunk).ToArray();
            return junk is null || junk.Length == 0 || !junk.Any(f => f.MessageCount.HasValue)
                ? null
                : junk.Sum(f => f.MessageCount ?? 0);
        }
    }

    public long? JunkSizeBytes
    {
        get
        {
            var junk = Folders?.Where(f => f.IsJunk).ToArray();
            return junk is null || !junk.Any(f => f.SizeBytes.HasValue)
                ? null
                : junk.Sum(f => f.SizeBytes ?? 0);
        }
    }
}

public sealed record MailboxFolderSnapshot(
    string Name,
    string FullName,
    bool IsInbox,
    bool IsJunk,
    bool IsSent,
    bool IsTrash,
    int? MessageCount,
    int? UnreadCount,
    long? SizeBytes);

/// <summary>
/// Down-only mailbox pressure feedback. Severity 1 slows global SEND pacing;
/// severity 2 applies a stronger slowdown and reduces adaptive concurrency.
/// </summary>
public sealed record MailboxCapacityFeedbackDecision(
    int Severity,
    double QuotaUsagePercent,
    long StorageGrowthBytes,
    string Reason);

/// <summary>
/// Converts consecutive, server-reported IMAP STORAGE observations into cautious
/// down-only pressure feedback. It never raises rate/concurrency and requires
/// repeated positive storage growth near the configured quota-stop threshold.
/// </summary>
public sealed class MailboxCapacityFeedbackController
{
    private MailboxMonitorSnapshot? _previous;
    private int _positiveGrowthWindows;
    private int _lastSeverity;

    public void SetBaseline(MailboxMonitorSnapshot? baseline)
    {
        _previous = baseline;
        _positiveGrowthWindows = 0;
        _lastSeverity = 0;
    }

    public MailboxCapacityFeedbackDecision? Observe(MailboxMonitorSnapshot snapshot, int stopThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (stopThresholdPercent is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(stopThresholdPercent));

        var previous = _previous;
        _previous = snapshot;
        if (previous is null
            || snapshot.CheckedAt <= previous.CheckedAt
            || !previous.QuotaUsedBytes.HasValue
            || !previous.QuotaUsagePercent.HasValue
            || !snapshot.QuotaUsedBytes.HasValue
            || !snapshot.QuotaUsagePercent.HasValue)
        {
            _positiveGrowthWindows = 0;
            return null;
        }

        var growth = snapshot.QuotaUsedBytes.Value - previous.QuotaUsedBytes.Value;
        _positiveGrowthWindows = growth > 0 ? _positiveGrowthWindows + 1 : 0;
        if (growth <= 0)
            return null;

        var warningThreshold = Math.Max(1, stopThresholdPercent - 10);
        var criticalThreshold = Math.Max(warningThreshold, stopThresholdPercent - 5);
        var severity = snapshot.QuotaUsagePercent.Value >= criticalThreshold
            ? 2
            : snapshot.QuotaUsagePercent.Value >= warningThreshold && _positiveGrowthWindows >= 2
                ? 1
                : 0;

        if (severity <= _lastSeverity)
            return null;

        _lastSeverity = severity;
        var reason = severity == 2
            ? $"IMAP quota is {snapshot.QuotaUsagePercent.Value:F1}% with continued storage growth ({growth:N0} B in the latest interval)."
            : $"IMAP quota is {snapshot.QuotaUsagePercent.Value:F1}% with repeated storage growth; reducing SEND rate.";
        return new MailboxCapacityFeedbackDecision(severity, snapshot.QuotaUsagePercent.Value, growth, reason);
    }
}

/// <summary>
/// Changes observed between the beginning and end of one SMTP run.
/// These deltas are independent mailbox observations, not proof that every
/// change was caused by this run; concurrent mailbox activity may contribute.
/// Null fields mean the server did not provide both measurements.
/// </summary>
public sealed record MailboxRunDelta(
    long? QuotaMessageCountDelta,
    long? QuotaStorageBytesDelta,
    long? InboxMessageCountDelta,
    long? JunkMessageCountDelta,
    DateTimeOffset BaselineCheckedAt,
    DateTimeOffset FinalCheckedAt,
    string Note);

public static class MailboxMonitorMath
{
    /// <summary>
    /// Calculates observed mailbox deltas only where both snapshots contain a measurement.
    /// It never substitutes zero for an unsupported quota/folder metric.
    /// </summary>
    public static MailboxRunDelta? CalculateRunDelta(MailboxMonitorSnapshot? baseline, MailboxMonitorSnapshot? final)
    {
        if (baseline is null || final is null || final.CheckedAt <= baseline.CheckedAt)
            return null;

        static long? Difference(long? before, long? after)
            => before.HasValue && after.HasValue ? after.Value - before.Value : null;

        return new MailboxRunDelta(
            QuotaMessageCountDelta: Difference(
                baseline.QuotaMessageCount.HasValue ? baseline.QuotaMessageCount.Value : null,
                final.QuotaMessageCount.HasValue ? final.QuotaMessageCount.Value : null),
            QuotaStorageBytesDelta: Difference(baseline.QuotaUsedBytes, final.QuotaUsedBytes),
            InboxMessageCountDelta: Difference(
                baseline.InboxMessageCount.HasValue ? baseline.InboxMessageCount.Value : null,
                final.InboxMessageCount.HasValue ? final.InboxMessageCount.Value : null),
            JunkMessageCountDelta: Difference(
                baseline.JunkMessageCount.HasValue ? baseline.JunkMessageCount.Value : null,
                final.JunkMessageCount.HasValue ? final.JunkMessageCount.Value : null),
            BaselineCheckedAt: baseline.CheckedAt,
            FinalCheckedAt: final.CheckedAt,
            Note: "Pozorovaná změna mezi dvěma IMAP snímky; nelze ji bez další korelace připsat výhradně tomuto SMTP běhu.");
    }

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