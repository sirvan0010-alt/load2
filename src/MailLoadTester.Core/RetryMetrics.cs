namespace MailLoadTester;

/// <summary>
/// A4: thread-safe retry observability. Not a limiter — only counts outcomes of the
/// existing per-message retry loop (connection released before backoff).
/// </summary>
public sealed class RetryMetrics
{
    long _retryableFailures;
    long _retryAttempts;
    long _successAfterRetry;
    long _exhausted;
    long _permanentFailures;
    long _budgetExhausted;
    readonly long[] _byAttempt = new long[8]; // index 1..7 = attempt number of the retry

    public void RecordRetryableFailure() => Interlocked.Increment(ref _retryableFailures);

    public void RecordRetryAttempt(int attemptNumber)
    {
        Interlocked.Increment(ref _retryAttempts);
        if (attemptNumber >= 1 && attemptNumber < _byAttempt.Length)
            Interlocked.Increment(ref _byAttempt[attemptNumber]);
        else if (attemptNumber >= _byAttempt.Length)
            Interlocked.Increment(ref _byAttempt[_byAttempt.Length - 1]);
    }

    public void RecordSuccessAfterRetry() => Interlocked.Increment(ref _successAfterRetry);

    public void RecordExhausted() => Interlocked.Increment(ref _exhausted);

    public void RecordPermanentFailure() => Interlocked.Increment(ref _permanentFailures);

    public void RecordBudgetExhausted() => Interlocked.Increment(ref _budgetExhausted);

    public RetryMetricsSnapshot Snapshot()
    {
        var dist = new int[_byAttempt.Length];
        for (var i = 0; i < _byAttempt.Length; i++)
            dist[i] = (int)Math.Max(0, Volatile.Read(ref _byAttempt[i]));
        return new RetryMetricsSnapshot(
            RetryableFailures: (int)Math.Max(0, Volatile.Read(ref _retryableFailures)),
            RetryAttempts: (int)Math.Max(0, Volatile.Read(ref _retryAttempts)),
            SuccessAfterRetry: (int)Math.Max(0, Volatile.Read(ref _successAfterRetry)),
            Exhausted: (int)Math.Max(0, Volatile.Read(ref _exhausted)),
            PermanentFailures: (int)Math.Max(0, Volatile.Read(ref _permanentFailures)),
            BudgetExhausted: (int)Math.Max(0, Volatile.Read(ref _budgetExhausted)),
            AttemptsByNumber: dist);
    }
}

/// <param name="AttemptsByNumber">Index = retry attempt number (1 = first retry). Index 0 unused.</param>
public sealed record RetryMetricsSnapshot(
    int RetryableFailures,
    int RetryAttempts,
    int SuccessAfterRetry,
    int Exhausted,
    int PermanentFailures,
    int BudgetExhausted,
    IReadOnlyList<int> AttemptsByNumber);
