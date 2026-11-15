namespace MailLoadTester;

/// <summary>
/// Adaptive concurrency gate. It reacts to SMTP-side pressure (temporary 4xx,
/// timeouts and transport failures) and sustained send-latency growth. Permanent
/// SMTP 5xx failures are counted as failures but do not by themselves imply
/// server overload. Thread-safe.
/// </summary>
public sealed class AdaptiveConcurrencyLimiter
{
    private readonly int _initial;
    private readonly int _min;
    private readonly int _max;
    private readonly object _lock = new();
    private int _current;
    private int _active;
    private int _windowAttempts;
    private int _windowFailures;
    private int _windowPressureAttempts;
    private int _windowLatencySamples;
    private double _windowLatencyTotalMs;
    private double _baselineLatencyMs;
    private DateTime _windowStart = DateTime.UtcNow;
    private readonly TimeSpan _window;
    private readonly Queue<TaskCompletionSource<bool>> _waiters = new();

    public int Current => Volatile.Read(ref _current);
    public int Active => Volatile.Read(ref _active);

    public AdaptiveConcurrencyLimiter(int initial, int min = 1, int max = 20, TimeSpan? evaluationWindow = null)
    {
        if (min < 1 || max < min) throw new ArgumentOutOfRangeException();
        _window = evaluationWindow ?? TimeSpan.FromSeconds(10);
        if (_window < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(evaluationWindow));
        _initial = Math.Clamp(initial, min, max);
        _min = min;
        _max = max;
        _current = _initial;
    }

    public Task AcquireAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        TaskCompletionSource<bool>? tcs = null;

        lock (_lock)
        {
            if (_active < _current)
            {
                _active++;
                return Task.CompletedTask;
            }

            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Enqueue(tcs);
        }

        if (!ct.CanBeCanceled)
            return tcs.Task;

        return WaitWithCancellationAsync(tcs, ct);
    }

    private async Task WaitWithCancellationAsync(TaskCompletionSource<bool> tcs, CancellationToken ct)
    {
        using var reg = ct.Register(static state =>
        {
            var (limiter, waiter) = ((AdaptiveConcurrencyLimiter, TaskCompletionSource<bool>))state!;
            limiter.TryCancelWaiter(waiter);
        }, (this, tcs));

        await tcs.Task.ConfigureAwait(false);
    }

    private void TryCancelWaiter(TaskCompletionSource<bool> waiter)
    {
        lock (_lock)
        {
            if (waiter.Task.IsCompleted) return;

            if (_waiters.Count > 0)
            {
                var kept = new Queue<TaskCompletionSource<bool>>(_waiters.Count);
                while (_waiters.Count > 0)
                {
                    var w = _waiters.Dequeue();
                    if (!ReferenceEquals(w, waiter))
                        kept.Enqueue(w);
                }
                while (kept.Count > 0)
                    _waiters.Enqueue(kept.Dequeue());
            }

            waiter.TrySetCanceled();
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            if (_active > 0)
                _active--;

            while (_waiters.Count > 0 && _active < _current)
            {
                var next = _waiters.Dequeue();
                if (next.Task.IsCompleted) continue;
                _active++;
                next.TrySetResult(true);
                break;
            }
        }
    }

    /// <summary>
    /// Compatibility overload: legacy callers treat every failed attempt as
    /// pressure. New SMTP call sites should pass measured latency and classify
    /// whether the failure indicates temporary server/transport pressure.
    /// </summary>
    public void RecordAttempt(bool success) =>
        RecordAttempt(success, smtpLatencyMs: 0, serverPressure: !success);

    /// <param name="success">Whether SMTP accepted the message.</param>
    /// <param name="smtpLatencyMs">Duration of the actual SMTP SendAsync call, in milliseconds.</param>
    /// <param name="serverPressure">
    /// True for temporary 4xx responses, timeouts and transport failures. False
    /// for permanent 5xx/policy failures that should not lower concurrency.
    /// </param>
    public void RecordAttempt(bool success, double smtpLatencyMs, bool serverPressure)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (now - _windowStart >= _window)
            {
                EvaluateWindow();
                _windowStart = now;
                _windowAttempts = 0;
                _windowFailures = 0;
                _windowPressureAttempts = 0;
                _windowLatencySamples = 0;
                _windowLatencyTotalMs = 0;
            }

            _windowAttempts++;
            if (!success)
            {
                _windowFailures++;
                if (serverPressure) _windowPressureAttempts++;
            }

            if (double.IsFinite(smtpLatencyMs) && smtpLatencyMs > 0)
            {
                _windowLatencySamples++;
                _windowLatencyTotalMs += smtpLatencyMs;
            }

            WakeWaiterIfCapacityAvailable();
        }
    }

    private void EvaluateWindow()
    {
        if (_windowAttempts < 5) return;

        var failureRate = (double)_windowFailures / _windowAttempts;
        var pressureRate = (double)_windowPressureAttempts / _windowAttempts;
        var averageLatency = _windowLatencySamples > 0
            ? _windowLatencyTotalMs / _windowLatencySamples
            : 0;

        // Latency is compared with a per-run baseline to avoid treating a
        // consistently slow server or large message as overload. Require both
        // a 2x increase and at least 1 second before reducing concurrency.
        var latencyPressure = averageLatency > 0 &&
            _baselineLatencyMs > 0 &&
            averageLatency >= Math.Max(1_000, _baselineLatencyMs * 2.0);

        if (pressureRate > 0.30)
            _current = Math.Max(_min, _current / 2);
        else if (latencyPressure)
            _current = Math.Max(_min, _current - 1);
        else if (failureRate < 0.05 && _current < _max)
            _current = Math.Min(_max, _current + 1);

        // Update baseline only when the window did not indicate congestion.
        if (averageLatency > 0 && !latencyPressure && pressureRate <= 0.30)
            _baselineLatencyMs = _baselineLatencyMs <= 0
                ? averageLatency
                : (_baselineLatencyMs * 0.8) + (averageLatency * 0.2);
    }

    private void WakeWaiterIfCapacityAvailable()
    {
        while (_waiters.Count > 0 && _active < _current)
        {
            var woken = _waiters.Dequeue();
            if (woken.Task.IsCompleted) continue;
            _active++;
            woken.TrySetResult(true);
            break;
        }
    }

    /// <summary>
    /// Reduces concurrency by one in response to independently observed mailbox
    /// storage pressure. Existing in-flight permits are allowed to finish.
    /// </summary>
    public void ApplyMailboxPressure()
    {
        lock (_lock)
        {
            if (_current > _min)
                _current--;
        }
    }

    public void Reset()
    {
        Queue<TaskCompletionSource<bool>> cancel;
        lock (_lock)
        {
            _current = _initial;
            _windowAttempts = 0;
            _windowFailures = 0;
            _windowPressureAttempts = 0;
            _windowLatencySamples = 0;
            _windowLatencyTotalMs = 0;
            _baselineLatencyMs = 0;
            _windowStart = DateTime.UtcNow;
            cancel = new Queue<TaskCompletionSource<bool>>(_waiters);
            _waiters.Clear();
        }
        while (cancel.Count > 0)
            cancel.Dequeue().TrySetCanceled();
    }
}
