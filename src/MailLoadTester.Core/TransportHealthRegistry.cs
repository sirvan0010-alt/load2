using System.Collections.Concurrent;

namespace MailLoadTester;

/// <summary>
/// FEAT-HEALTH: thread-safe per-endpoint health with quarantine/recovery.
/// Key is typically "host:port" or a proxy display key — not credentials.
/// </summary>
public enum EndpointHealthState
{
    Healthy = 0,
    Degraded = 1,
    Quarantined = 2
}

public readonly record struct EndpointHealthSnapshot(
    string Key,
    EndpointHealthState State,
    int ConsecutiveFailures,
    int TotalSuccesses,
    int TotalFailures,
    DateTimeOffset? QuarantinedUntil,
    string? LastFailureCategory);

public sealed class TransportHealthRegistry
{
    private readonly ConcurrentDictionary<string, Slot> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _degradedAfter;
    private readonly int _quarantineAfter;
    private readonly TimeSpan _quarantineDuration;

    public TransportHealthRegistry(
        int degradedAfter = 2,
        int quarantineAfter = 5,
        TimeSpan? quarantineDuration = null)
    {
        if (degradedAfter < 1) throw new ArgumentOutOfRangeException(nameof(degradedAfter));
        if (quarantineAfter < degradedAfter) throw new ArgumentOutOfRangeException(nameof(quarantineAfter));
        _degradedAfter = degradedAfter;
        _quarantineAfter = quarantineAfter;
        _quarantineDuration = quarantineDuration ?? TimeSpan.FromMinutes(2);
    }

    public void RecordSuccess(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var slot = _slots.GetOrAdd(key, static k => new Slot(k));
        lock (slot.Gate)
        {
            slot.TotalSuccesses++;
            slot.ConsecutiveFailures = 0;
            slot.LastFailureCategory = null;
            slot.QuarantinedUntil = null;
        }
    }

    public void RecordFailure(string key, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        var slot = _slots.GetOrAdd(key, static k => new Slot(k));
        lock (slot.Gate)
        {
            slot.TotalFailures++;
            slot.ConsecutiveFailures++;
            if (!string.IsNullOrEmpty(category))
                slot.LastFailureCategory = category;
            if (slot.ConsecutiveFailures >= _quarantineAfter)
                slot.QuarantinedUntil = DateTimeOffset.UtcNow + _quarantineDuration;
        }
    }

    public bool IsAvailable(string key, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return true;
        if (!_slots.TryGetValue(key, out var slot)) return true;
        var t = now ?? DateTimeOffset.UtcNow;
        lock (slot.Gate)
        {
            if (slot.QuarantinedUntil is { } until && until > t)
                return false;
            return true;
        }
    }

    public EndpointHealthState GetState(string key, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(key) || !_slots.TryGetValue(key, out var slot))
            return EndpointHealthState.Healthy;
        var t = now ?? DateTimeOffset.UtcNow;
        lock (slot.Gate)
        {
            if (slot.QuarantinedUntil is { } until && until > t)
                return EndpointHealthState.Quarantined;
            if (slot.ConsecutiveFailures >= _degradedAfter)
                return EndpointHealthState.Degraded;
            return EndpointHealthState.Healthy;
        }
    }

    public EndpointHealthSnapshot Snapshot(string key, DateTimeOffset? now = null)
    {
        key ??= "";
        if (!_slots.TryGetValue(key, out var slot))
            return new EndpointHealthSnapshot(key, EndpointHealthState.Healthy, 0, 0, 0, null, null);
        var t = now ?? DateTimeOffset.UtcNow;
        lock (slot.Gate)
        {
            var state = EndpointHealthState.Healthy;
            DateTimeOffset? qUntil = slot.QuarantinedUntil;
            if (qUntil is { } until && until > t)
                state = EndpointHealthState.Quarantined;
            else if (slot.ConsecutiveFailures >= _degradedAfter)
                state = EndpointHealthState.Degraded;
            else
                qUntil = null;
            return new EndpointHealthSnapshot(
                key, state, slot.ConsecutiveFailures, slot.TotalSuccesses, slot.TotalFailures,
                state == EndpointHealthState.Quarantined ? qUntil : null,
                slot.LastFailureCategory);
        }
    }

    public IReadOnlyList<EndpointHealthSnapshot> SnapshotAll(DateTimeOffset? now = null)
    {
        var t = now ?? DateTimeOffset.UtcNow;
        var list = new List<EndpointHealthSnapshot>();
        foreach (var key in _slots.Keys)
            list.Add(Snapshot(key, t));
        return list;
    }

    public static string ClassifyFailure(Exception ex)
    {
        if (ex is MailKit.Net.Smtp.SmtpCommandException sce)
            return $"smtp_{(int)sce.StatusCode / 100}xx";
        if (ex is TimeoutException) return "timeout";
        if (ex is IOException) return "io";
        if (ex is MailKit.Security.AuthenticationException) return "auth";
        if (ex is OperationCanceledException) return "cancelled";
        return ex.GetType().Name;
    }

    private sealed class Slot
    {
        public readonly object Gate = new();
        public readonly string Key;
        public int ConsecutiveFailures;
        public int TotalSuccesses;
        public int TotalFailures;
        public DateTimeOffset? QuarantinedUntil;
        public string? LastFailureCategory;
        public Slot(string key) => Key = key;
    }
}
