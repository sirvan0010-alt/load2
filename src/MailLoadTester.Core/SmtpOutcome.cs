using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailLoadTester;

/// <summary>
/// A5: single outcome taxonomy for SMTP results, retry, health, report and GUI.
/// </summary>
public enum SmtpOutcome
{
    Success,
    Transient,
    Throttled,
    Timeout,
    Authentication,
    PolicyRejected,
    Recipient,
    Permanent,
    Cancelled,
    Unknown
}

/// <summary>
/// Central classifier. RetryPolicy, TransportHealth and counters must consume this — not ad-hoc status checks.
/// </summary>
public static class SmtpOutcomeClassifier
{
    public static SmtpOutcome Classify(Exception? ex)
    {
        if (ex is null) return SmtpOutcome.Unknown;
        if (ex is OperationCanceledException) return SmtpOutcome.Cancelled;
        if (ex is TimeoutException) return SmtpOutcome.Timeout;
        if (ex is AuthenticationException) return SmtpOutcome.Authentication;
        if (ex is SmtpCommandException sce)
            return ClassifySmtp((int)sce.StatusCode, sce.Message);
        if (ex is SmtpProtocolException or IOException)
            return SmtpOutcome.Transient;
        // Nested (e.g. AggregateException)
        if (ex.InnerException is not null && ex is not SmtpCommandException)
        {
            var inner = Classify(ex.InnerException);
            if (inner is not SmtpOutcome.Unknown)
                return inner;
        }
        return SmtpOutcome.Unknown;
    }

    public static SmtpOutcome ClassifySmtp(int code, string? text)
    {
        var t = (text ?? "").ToLowerInvariant();

        if (code is >= 200 and < 300)
            return SmtpOutcome.Success;

        if (code is >= 400 and < 500)
        {
            if (IsThrottleText(t) || code is 421 or 450)
            {
                if (IsGreylistText(t))
                    return SmtpOutcome.Transient; // greylist: retryable wait, not pure rate throttle
                if (IsThrottleText(t))
                    return SmtpOutcome.Throttled;
            }
            if (IsGreylistText(t))
                return SmtpOutcome.Transient;
            return SmtpOutcome.Transient;
        }

        if (code is >= 500 and < 600)
        {
            if (code is 530 or 535 || t.Contains("auth") || t.Contains("credential") || t.Contains("login"))
                return SmtpOutcome.Authentication;
            if (code is 550 or 551 or 552 or 553 || t.Contains("user unknown") || t.Contains("mailbox") && t.Contains("exist"))
                return SmtpOutcome.Recipient;
            if (code is 554 || t.Contains("policy") || t.Contains("spam") || t.Contains("rejected") || t.Contains("blocked"))
                return SmtpOutcome.PolicyRejected;
            return SmtpOutcome.Permanent;
        }

        return SmtpOutcome.Unknown;
    }

    /// <summary>Whether the runner may retry this outcome under MaxRetries / budget.</summary>
    public static bool IsRetryable(SmtpOutcome outcome) => outcome is
        SmtpOutcome.Transient or
        SmtpOutcome.Throttled or
        SmtpOutcome.Timeout;

    public static bool IsRetryable(Exception ex) => IsRetryable(Classify(ex));

    /// <summary>Short category string for endpoint health snapshots (stable keys).</summary>
    public static string ToHealthCategory(SmtpOutcome outcome) => outcome switch
    {
        SmtpOutcome.Success => "success",
        SmtpOutcome.Transient => "transient",
        SmtpOutcome.Throttled => "throttled",
        SmtpOutcome.Timeout => "timeout",
        SmtpOutcome.Authentication => "auth",
        SmtpOutcome.PolicyRejected => "policy",
        SmtpOutcome.Recipient => "recipient",
        SmtpOutcome.Permanent => "permanent",
        SmtpOutcome.Cancelled => "cancelled",
        _ => "unknown"
    };

    public static string ToHealthCategory(Exception ex) => ToHealthCategory(Classify(ex));

    static bool IsGreylistText(string t) =>
        t.Contains("greylist") || t.Contains("graylist") || t.Contains("try again later") ||
        t.Contains("try later") || t.Contains("deferred") || t.Contains("temporarily deferred");

    static bool IsThrottleText(string t) =>
        t.Contains("rate") || t.Contains("limit") || t.Contains("too many") ||
        t.Contains("throttle") || t.Contains("slow down") || t.Contains("421-4.7");
}

/// <summary>A5: thread-safe aggregate counts for one run (not a parallel telemetry system).</summary>
public sealed class SmtpOutcomeCounters
{
    long _success, _transient, _throttled, _timeout, _auth, _policy, _recipient, _permanent, _cancelled, _unknown;

    public void Record(SmtpOutcome outcome)
    {
        switch (outcome)
        {
            case SmtpOutcome.Success: Interlocked.Increment(ref _success); break;
            case SmtpOutcome.Transient: Interlocked.Increment(ref _transient); break;
            case SmtpOutcome.Throttled: Interlocked.Increment(ref _throttled); break;
            case SmtpOutcome.Timeout: Interlocked.Increment(ref _timeout); break;
            case SmtpOutcome.Authentication: Interlocked.Increment(ref _auth); break;
            case SmtpOutcome.PolicyRejected: Interlocked.Increment(ref _policy); break;
            case SmtpOutcome.Recipient: Interlocked.Increment(ref _recipient); break;
            case SmtpOutcome.Permanent: Interlocked.Increment(ref _permanent); break;
            case SmtpOutcome.Cancelled: Interlocked.Increment(ref _cancelled); break;
            default: Interlocked.Increment(ref _unknown); break;
        }
    }

    public void Record(Exception? ex) => Record(SmtpOutcomeClassifier.Classify(ex));

    public SmtpOutcomeCountsSnapshot Snapshot() => new(
        Success: (int)Math.Max(0, Volatile.Read(ref _success)),
        Transient: (int)Math.Max(0, Volatile.Read(ref _transient)),
        Throttled: (int)Math.Max(0, Volatile.Read(ref _throttled)),
        Timeout: (int)Math.Max(0, Volatile.Read(ref _timeout)),
        Authentication: (int)Math.Max(0, Volatile.Read(ref _auth)),
        PolicyRejected: (int)Math.Max(0, Volatile.Read(ref _policy)),
        Recipient: (int)Math.Max(0, Volatile.Read(ref _recipient)),
        Permanent: (int)Math.Max(0, Volatile.Read(ref _permanent)),
        Cancelled: (int)Math.Max(0, Volatile.Read(ref _cancelled)),
        Unknown: (int)Math.Max(0, Volatile.Read(ref _unknown)));
}

public sealed record SmtpOutcomeCountsSnapshot(
    int Success,
    int Transient,
    int Throttled,
    int Timeout,
    int Authentication,
    int PolicyRejected,
    int Recipient,
    int Permanent,
    int Cancelled,
    int Unknown);
