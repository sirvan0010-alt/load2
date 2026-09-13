using System.Collections.Concurrent;
using System.Diagnostics;

namespace MailLoadTester;

public sealed class SmartPaceController
{
    private readonly MailTestOptions _options;
    private readonly ProviderThrottleSettings _providerThrottle;
    private readonly object _lock = new();
    /// <summary>Serializes actual SEND spacing so concurrent workers cannot both claim the same slot.</summary>
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private int _successCount;
    private int _messagesInCurrentBurst;
    private int _currentIntervalMs;
    private int _warmupPhaseIndex;
    private int _warmupMessagesInPhase;
    private readonly ConcurrentDictionary<string, RecipientWindow> _recipientWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RecipientWindow> _providerWindows = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _burstPauseUntil = DateTimeOffset.MinValue;
    private DateTimeOffset _greylistPauseUntil = DateTimeOffset.MinValue;
    private int _greylistHits;
    private readonly LinkedList<long> _schedule = new();
    private readonly long _freq = Stopwatch.Frequency;
    private long _lastActualSendTicks;

    private sealed class RecipientWindow
    {
        public Queue<DateTimeOffset> Committed = new();
        public int Reserved;
    }

    internal int ScheduledReservationCount
    {
        get { lock (_lock) return _schedule.Count; }
    }

    private TimeSpan RecipientWindowSpan =>
        TimeSpan.FromMinutes(Math.Max(1, _options.PerRecipientWindowMinutes));

    private TimeSpan ProviderWindowSpan =>
        TimeSpan.FromMinutes(Math.Max(1, _providerThrottle.WindowMinutes));

    private static void PurgeCommitted(RecipientWindow win, DateTimeOffset now, TimeSpan window)
    {
        while (win.Committed.Count > 0 && now - win.Committed.Peek() >= window)
            win.Committed.Dequeue();
    }

    public SmartPaceController(MailTestOptions options, ProviderThrottleSettings? providerThrottle = null)
    {
        _options = options;
        _providerThrottle = providerThrottle ?? ProviderThrottleSettings.FromEnvironment();
        _currentIntervalMs = Math.Max(0, options.IntervalMs);
    }

    public int CurrentIntervalMs
    {
        get { lock (_lock) return _currentIntervalMs; }
    }

    public string StatusText
    {
        get
        {
            lock (_lock)
            {
                var now = DateTimeOffset.UtcNow;
                if (now < _greylistPauseUntil)
                    return $"Greylist odklad – pokračuji v {_greylistPauseUntil.LocalDateTime:HH:mm:ss}";
                if (now < _burstPauseUntil)
                    return $"Pauza po burstu – pokračuji v {_burstPauseUntil.LocalDateTime:HH:mm:ss}";
                if (_options.EnableSendingTimeWindow && !IsInsideSendingWindow(now))
                    return "Čekám na časové okno odesílání";
                if (_options.EnableWarmup && _warmupPhaseIndex < GetWarmupPhases().Length)
                    return $"Warm-up fáze {_warmupPhaseIndex + 1}/{GetWarmupPhases().Length}";
                return "Běží";
            }
        }
    }

    public async Task WaitBeforeSendAsync(string recipient, CancellationToken ct)
    {
        var providerKey = GetProviderKey(recipient);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            TimeSpan block;
            lock (_lock) { block = ComputeAbsoluteBlock(recipient, providerKey); }
            if (block <= TimeSpan.Zero) break;
            var slice = TimeSpan.FromMilliseconds(Math.Min(500, block.TotalMilliseconds));
            await Task.Delay(slice, ct).ConfigureAwait(false);
        }

        if (_options.EnablePerRecipientLimit)
        {
            while (!TryReserveRecipient(recipient))
            {
                ct.ThrowIfCancellationRequested();
                TimeSpan block;
                lock (_lock) { block = ComputeAbsoluteBlock(recipient, providerKey); }
                if (block <= TimeSpan.Zero) block = TimeSpan.FromMilliseconds(50);
                var slice = TimeSpan.FromMilliseconds(Math.Min(500, block.TotalMilliseconds));
                await Task.Delay(slice, ct).ConfigureAwait(false);
            }
        }

        if (_providerThrottle.Enabled)
        {
            while (!TryReserveProvider(providerKey))
            {
                ct.ThrowIfCancellationRequested();
                TimeSpan block;
                lock (_lock) { block = ComputeProviderBlock(providerKey); }
                if (block <= TimeSpan.Zero) block = TimeSpan.FromMilliseconds(50);
                var slice = TimeSpan.FromMilliseconds(Math.Min(500, block.TotalMilliseconds));
                await Task.Delay(slice, ct).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask<SendPaceLease> AcquireSendSlotAsync(CancellationToken ct)
    {
        await _sendGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                TimeSpan delay;
                lock (_lock)
                {
                    var intervalMs = _currentIntervalMs;
                    if (_options.EnableWarmup && intervalMs > 0)
                    {
                        var phases = GetWarmupPhases();
                        if (_warmupPhaseIndex < phases.Length)
                        {
                            var factor = 1.0 + (phases.Length - _warmupPhaseIndex) * 0.5;
                            intervalMs = (int)(intervalMs * factor);
                        }
                    }
                    if (_options.EnableJitter && _options.JitterPercent > 0 && intervalMs > 0)
                    {
                        var jitter = _options.JitterPercent / 100.0;
                        var factor = 1.0 + (Random.Shared.NextDouble() * 2 - 1) * jitter;
                        intervalMs = (int)Math.Max(0, intervalMs * factor);
                    }

                    if (intervalMs <= 0 || _lastActualSendTicks == 0)
                    {
                        _lastActualSendTicks = Stopwatch.GetTimestamp();
                        return new SendPaceLease(this);
                    }

                    var now = Stopwatch.GetTimestamp();
                    var intervalTicks = (long)(intervalMs * (double)_freq / 1000.0);
                    var earliest = _lastActualSendTicks + intervalTicks;
                    if (now >= earliest)
                    {
                        _lastActualSendTicks = now;
                        return new SendPaceLease(this);
                    }

                    var waitTicks = earliest - now;
                    delay = TimeSpan.FromMilliseconds(Math.Max(1, waitTicks * 1000.0 / _freq));
                }
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        catch
        {
            _sendGate.Release();
            throw;
        }
    }

    public sealed class SendPaceLease : IAsyncDisposable, IDisposable
    {
        private SmartPaceController? _owner;

        internal SendPaceLease(SmartPaceController owner) => _owner = owner;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?._sendGate.Release();
        }
    }

    private TimeSpan ComputeAbsoluteBlock(string recipient, string providerKey)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _greylistPauseUntil) return _greylistPauseUntil - now;
        if (now < _burstPauseUntil) return _burstPauseUntil - now;
        if (_options.EnableSendingTimeWindow && !IsInsideSendingWindow(now))
            return NextWindowStart(now) - now;
        if (_options.EnablePerRecipientLimit)
        {
            var key = recipient.Trim().ToLowerInvariant();
            if (_recipientWindows.TryGetValue(key, out var win))
            {
                PurgeCommitted(win, now, RecipientWindowSpan);
                if (win.Committed.Count + win.Reserved >= _options.MaxMessagesPerRecipient && win.Committed.Count > 0)
                    return win.Committed.Peek() + RecipientWindowSpan - now;
            }
        }
        if (_providerThrottle.Enabled)
            return ComputeProviderBlock(providerKey);
        return TimeSpan.Zero;
    }

    private TimeSpan ComputeProviderBlock(string providerKey)
    {
        var now = DateTimeOffset.UtcNow;
        if (!_providerWindows.TryGetValue(providerKey, out var win))
            return TimeSpan.Zero;
        PurgeCommitted(win, now, ProviderWindowSpan);
        if (win.Committed.Count + win.Reserved >= _providerThrottle.MaxMessages && win.Committed.Count > 0)
            return win.Committed.Peek() + ProviderWindowSpan - now;
        return TimeSpan.Zero;
    }

    private static string GetProviderKey(string recipient)
    {
        var value = recipient.Trim();
        var at = value.LastIndexOf('@');
        if (at < 0 || at == value.Length - 1)
            return value.ToLowerInvariant();
        return value[(at + 1)..].TrimEnd('.').ToLowerInvariant();
    }

    public bool TryReserveRecipient(string recipient)
    {
        if (!_options.EnablePerRecipientLimit) return true;
        var key = recipient.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            var win = _recipientWindows.GetOrAdd(key, _ => new RecipientWindow());
            PurgeCommitted(win, now, RecipientWindowSpan);
            if (win.Committed.Count + win.Reserved >= _options.MaxMessagesPerRecipient)
                return false;
            win.Reserved++;
            return true;
        }
    }

    private bool TryReserveProvider(string providerKey)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            var win = _providerWindows.GetOrAdd(providerKey, _ => new RecipientWindow());
            PurgeCommitted(win, now, ProviderWindowSpan);
            if (win.Committed.Count + win.Reserved >= _providerThrottle.MaxMessages)
                return false;
            win.Reserved++;
            return true;
        }
    }

    public void CommitRecipient(string recipient)
    {
        var key = recipient.Trim().ToLowerInvariant();
        lock (_lock)
        {
            if (_options.EnablePerRecipientLimit && _recipientWindows.TryGetValue(key, out var win))
            {
                var now = DateTimeOffset.UtcNow;
                PurgeCommitted(win, now, RecipientWindowSpan);
                if (win.Reserved > 0) win.Reserved--;
                win.Committed.Enqueue(now);
            }

            if (_providerThrottle.Enabled)
                CommitProviderLocked(GetProviderKey(recipient));
        }
    }

    public void ReleaseRecipient(string recipient)
    {
        var key = recipient.Trim().ToLowerInvariant();
        lock (_lock)
        {
            if (_options.EnablePerRecipientLimit && _recipientWindows.TryGetValue(key, out var win) && win.Reserved > 0)
                win.Reserved--;

            if (_providerThrottle.Enabled && _providerWindows.TryGetValue(GetProviderKey(recipient), out var providerWin) && providerWin.Reserved > 0)
                providerWin.Reserved--;
        }
    }

    public void RecordSuccess(string recipient)
    {
        lock (_lock)
        {
            _successCount++;
            _messagesInCurrentBurst++;
            if (_options.EnableWarmup)
            {
                _warmupMessagesInPhase++;
                var phases = GetWarmupPhases();
                if (_warmupPhaseIndex < phases.Length && _warmupMessagesInPhase >= phases[_warmupPhaseIndex])
                {
                    _warmupPhaseIndex++;
                    _warmupMessagesInPhase = 0;
                }
            }
            if (_options.EnableProgressiveBackoff &&
                _options.BackoffAfterSuccesses > 0 &&
                _successCount % _options.BackoffAfterSuccesses == 0)
            {
                var next = (int)Math.Min(_options.MaxIntervalMs, _currentIntervalMs * _options.BackoffMultiplier);
                _currentIntervalMs = Math.Max(_currentIntervalMs, next);
            }
            if (_options.EnableBurstMode && _messagesInCurrentBurst >= _options.BurstSize)
            {
                _messagesInCurrentBurst = 0;
                _burstPauseUntil = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _options.BurstPauseSeconds));
            }
        }
    }

    private void CommitProviderLocked(string providerKey)
    {
        if (!_providerWindows.TryGetValue(providerKey, out var win)) return;
        var now = DateTimeOffset.UtcNow;
        PurgeCommitted(win, now, ProviderWindowSpan);
        if (win.Reserved > 0) win.Reserved--;
        win.Committed.Enqueue(now);
    }

    public void RecordGreylist()
    {
        if (!_options.DetectGreylist) return;
        lock (_lock)
        {
            if (_options.MaxGreylistRetries > 0 && _greylistHits >= _options.MaxGreylistRetries)
                return;
            _greylistHits++;
            _greylistPauseUntil = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.GreylistRetryMinutes));
        }
    }

    public void RecordTransientThrottle()
    {
        lock (_lock)
        {
            if (_options.EnableProgressiveBackoff)
            {
                var next = (int)Math.Min(_options.MaxIntervalMs, _currentIntervalMs * Math.Max(1.2, _options.BackoffMultiplier));
                _currentIntervalMs = Math.Max(_currentIntervalMs, next);
            }
        }
    }

    private bool IsInsideSendingWindow(DateTimeOffset now)
    {
        var hour = now.ToLocalTime().Hour;
        var from = Math.Clamp(_options.SendingWindowFromHour, 0, 23);
        var to = Math.Clamp(_options.SendingWindowToHour, 0, 23);
        if (from == to) return true;
        if (from < to) return hour >= from && hour < to;
        return hour >= from || hour < to;
    }

    private DateTimeOffset NextWindowStart(DateTimeOffset now)
    {
        var local = now.ToLocalTime();
        var from = Math.Clamp(_options.SendingWindowFromHour, 0, 23);
        var candidate = new DateTimeOffset(local.Year, local.Month, local.Day, from, 0, 0, local.Offset);
        if (candidate <= local) candidate = candidate.AddDays(1);
        return candidate.ToUniversalTime();
    }

    private int[]? _warmupPhasesCache;
    private int[] GetWarmupPhases() => _warmupPhasesCache ??= ParseWarmupPhases();
    private int[] ParseWarmupPhases()
    {
        if (string.IsNullOrWhiteSpace(_options.WarmupPhases))
            return new[] { 50, 150, 500 };
        return _options.WarmupPhases
            .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .DefaultIfEmpty(50)
            .ToArray();
    }
}
