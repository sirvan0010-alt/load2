namespace MailLoadTester;

/// <summary>
/// A4/A5: retry decision + backoff. Retryability comes from <see cref="SmtpOutcomeClassifier"/> (A5).
/// </summary>
public static class RetryPolicy
{
    /// <summary>Delegates to unified classifier — do not duplicate status-code rules here.</summary>
    public static bool IsRetryable(Exception ex) => SmtpOutcomeClassifier.IsRetryable(ex);

    /// <summary>Exponential backoff with jitter; capped. Honours CancellationToken at await site.</summary>
    public static TimeSpan GetDelay(int attempt)
    {
        var baseMs = Math.Min(8_000, 500 * Math.Pow(2, Math.Max(0, attempt)));
        var jitter = Random.Shared.NextDouble() * 0.4 - 0.2;
        return TimeSpan.FromMilliseconds(Math.Max(100, baseMs * (1 + jitter)));
    }

    /// <summary>Hard per-message attempt count: initial try + MaxRetries retries.</summary>
    public static int MaxAttempts(int maxRetries) => Math.Max(0, maxRetries) + 1;

    /// <summary>Global budget across the run to prevent retry storms.</summary>
    public static int GlobalBudget(int maxRetries, int messageCount) =>
        Math.Max(0, maxRetries) * Math.Max(1, messageCount);
}
