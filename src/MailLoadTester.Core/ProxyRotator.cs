using System.Collections.Concurrent;

namespace MailLoadTester;

/// <summary>
/// Round-robin / random výběr proxy s dočasným vyřazením po detekci banu.
/// Thread-safe. Random mode samples only from currently eligible endpoints
/// (BUG-004: never repeatedly draws blocked entries from the full set).
/// </summary>
public sealed class ProxyRotator
{
    private readonly ProxyEndpoint[] _all;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _blockedUntil = new(StringComparer.OrdinalIgnoreCase);
    private int _rr = -1;
    private readonly bool _random;
    private readonly TimeSpan _blockDuration;

    public ProxyRotator(IEnumerable<ProxyEndpoint> endpoints, bool random = false, TimeSpan? blockDuration = null)
    {
        _all = endpoints.ToArray();
        if (_all.Length == 0)
            throw new ArgumentException("Proxy seznam je prázdný.");
        _random = random;
        _blockDuration = blockDuration ?? TimeSpan.FromMinutes(15);
    }

    public int Count => _all.Length;
    public int BlockedCount => _blockedUntil.Count(kv => kv.Value > DateTimeOffset.UtcNow);

    public ProxyEndpoint? TryGetNext()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _blockedUntil)
        {
            if (kv.Value <= now)
                _blockedUntil.TryRemove(kv.Key, out _);
        }

        if (_random)
            return PickRandomEligible(now);

        for (int attempt = 0; attempt < _all.Length; attempt++)
        {
            var i = Interlocked.Increment(ref _rr);
            if (i < 0) { Interlocked.Exchange(ref _rr, 0); i = 0; }
            var ep = _all[i % _all.Length];
            if (!IsBlocked(ep.DisplayKey, now))
                return ep;
        }
        return null;
    }

    /// <summary>
    /// Uniform sample among currently unblocked endpoints only.
    /// If every endpoint is blocked, returns null (exhaustion) — never a blocked one.
    /// </summary>
    ProxyEndpoint? PickRandomEligible(DateTimeOffset now)
    {
        ProxyEndpoint? chosen = null;
        var eligible = 0;
        foreach (var ep in _all)
        {
            if (IsBlocked(ep.DisplayKey, now))
                continue;
            eligible++;
            // Reservoir sample of size 1 over eligible set.
            if (Random.Shared.Next(eligible) == 0)
                chosen = ep;
        }
        return chosen;
    }

    public void ReportBlocked(ProxyEndpoint ep, TimeSpan? duration = null)
    {
        var until = DateTimeOffset.UtcNow + (duration ?? _blockDuration);
        _blockedUntil[ep.DisplayKey] = until;
    }

    public void ReportBlockedKey(string displayKey, TimeSpan? duration = null)
    {
        if (string.IsNullOrEmpty(displayKey)) return;
        _blockedUntil[displayKey] = DateTimeOffset.UtcNow + (duration ?? _blockDuration);
    }

    bool IsBlocked(string key, DateTimeOffset now) =>
        _blockedUntil.TryGetValue(key, out var until) && until > now;
}
