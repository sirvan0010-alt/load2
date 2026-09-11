using System.Collections.Concurrent;
using System.Diagnostics;

namespace MailLoadTester;

public sealed class SmartPaceController
{
    private readonly MailTestOptions _options;
    private readonly object _lock = new();
    private int _successCount;
    private int _messagesInCurrentBurst;
    private int _currentIntervalMs;
    private int _warmupPhaseIndex;
    private int _warmupMessagesInPhase;
    private readonly ConcurrentDictionary<string, RecipientWindow> _recipientWindows = new(StringComparer.OrdinalIgnoreCase);
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

    private static void PurgeCommitted(RecipientWindow win, DateTimeOffset now, TimeSpan window)
    {
        while (win.Committed.Count > 0 && now - win.Committed.Peek() >= window)
            win.Committed.Dequeue();
    }

    public SmartPaceController(MailTestOptions options)
    {
        _options = options;
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
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            TimeSpan block;
            lock (_lock) { block = ComputeAbsoluteBlock(recipient); }
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
                lock (_lock) { block = ComputeAbsoluteBlock(recipient); }
                if (block <= TimeSpan.Zero) block = TimeSpan.FromMilliseconds(50);
                var slice = TimeSpan.FromMilliseconds(Math.Min(500, block.TotalMilliseconds));
                await Task.Delay(slice, ct).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask<SendPaceLease> AcquireSendSlotAsync(CancellationToken ct)
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
                    return new SendPaceLease();
                }

                var now = Stopwatch.GetTimestamp();
                var intervalTicks = (long)(intervalMs * (double)_freq / 1000.0);
                var earliest = _lastActualSendTicks + intervalTicks;
                if (now >= earliest)
                {
                    _lastActualSendTicks = now;
                    return new SendPaceLease();
                }

                var waitTicks = earliest - now;
                delay = TimeSpan.FromMilliseconds(Math.Max(1, waitTicks * 1000.0 / _freq));
            }
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    public sealed class SendPaceLease : IAsyncDisposable, IDisposable
    {
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private TimeSpan ComputeAbsoluteBlock(string recipient)
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
        return TimeSpan.Zero;
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

    public void CommitRecipient(string recipient)
    {
        if (!_options.EnablePerRecipientLimit) return;
        var key = recipient.Trim().ToLowerInvariant();
        lock (_lock)
        {
            if (!_recipientWindows.TryGetValue(key, out var win)) return;
            var now = DateTimeOffset.UtcNow;
            PurgeCommitted(win, now, RecipientWindowSpan);
            if (win.Reserved > 0) win.Reserved--;
            win.Committed.Enqueue(now);
        }
    }

    public void ReleaseRecipient(string recipient)
    {
        if (!_options.EnablePerRecipientLimit) return;
        var key = recipient.Trim().ToLowerInvariant();
        lock (_lock)
        {
            if (!_recipientWindows.TryGetValue(key, out var win)) return;
            if (win.Reserved > 0) win.Reserved--;
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
