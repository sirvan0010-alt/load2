using MailKit.Net.Smtp;

namespace MailLoadTester;

/// <summary>
/// A4: minimal retry decision + backoff. Full SMTP classification taxonomy is A5;
/// this only answers "may we retry this exception?" without a second limiter.
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Transient / potentially recoverable failures. Permanent 5xx, auth policy rejects,
    /// and unknown errors are not retryable here.
    /// </summary>
    public static bool IsRetryable(Exception ex)
    {
        if (ex is OperationCanceledException) return false;
        if (ex is SmtpCommandException sce)
        {
            var code = (int)sce.StatusCode;
            // 4xx = transient; 5xx = permanent for A4 (A5 may refine Throttled vs Policy).
            return code is >= 400 and < 500;
        }
        if (ex is SmtpProtocolException) return true;
        if (ex is IOException or TimeoutException) return true;
        return false;
    }

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
