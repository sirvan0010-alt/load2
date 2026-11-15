namespace MailLoadTester;

/// <summary>
/// Explicit operator guardrails for a bounded capacity run.
/// TargetMimeBytes counts each serialized MIME send attempt, including retries.
/// It is not a measurement of SMTP wire bytes or mailbox storage growth.
/// Zero byte/rate limits mean that the corresponding limit is disabled.
/// </summary>
public sealed record CapacityRunLimits(
    bool Enabled = false,
    int MaxMessageCount = 1000,
    long TargetMimeBytes = 0,
    long MaxMimeMessageBytes = 0,
    int MaxMessagesPerMinute = 0);

/// <summary>
/// Applies deterministic count and global-rate ceilings before a run starts.
/// Byte ceilings are enforced against the serialized MIME message immediately
/// before each SMTP SEND by <see cref="CapacityRunGuard"/>.
/// </summary>
public static class CapacityRunPlanner
{
    public static MailTestOptions Apply(MailTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var limits = options.CapacityRun;
        if (limits is not { Enabled: true })
            return options;

        var messageCount = Math.Min(options.MessageCount, limits.MaxMessageCount);
        var intervalMs = options.IntervalMs;
        if (limits.MaxMessagesPerMinute > 0)
        {
            var minimumInterval = (int)Math.Ceiling(60_000d / limits.MaxMessagesPerMinute);
            intervalMs = Math.Max(intervalMs, minimumInterval);
        }

        return options with { MessageCount = messageCount, IntervalMs = intervalMs };
    }
}

/// <summary>
/// Thread-safe cumulative MIME byte budget shared by all workers and auto-restart
/// attempts in one logical run. Reservations are conservative: once a SEND attempt
/// is admitted, its bytes remain charged even if the server later rejects or times out.
/// </summary>
public sealed class CapacityRunGuard
{
    private readonly CapacityRunLimits _limits;
    private readonly object _gate = new();
    private long _reservedMimeBytes;
    private int _reservedAttempts;

    public CapacityRunGuard(CapacityRunLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (!limits.Enabled)
            throw new ArgumentException("Capacity run guard requires Enabled=true.", nameof(limits));
        _limits = limits;
    }

    public long ReservedMimeBytes
    {
        get { lock (_gate) return _reservedMimeBytes; }
    }

    public int ReservedAttempts
    {
        get { lock (_gate) return _reservedAttempts; }
    }

    public bool TryReserve(long mimeMessageBytes, out string reason)
    {
        if (mimeMessageBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(mimeMessageBytes));

        lock (_gate)
        {
            if (_limits.MaxMimeMessageBytes > 0 && mimeMessageBytes > _limits.MaxMimeMessageBytes)
            {
                reason = $"Capacity stop: MIME message is {mimeMessageBytes:N0} B, above the configured per-message ceiling of {_limits.MaxMimeMessageBytes:N0} B.";
                return false;
            }

            if (_limits.TargetMimeBytes > 0
                && mimeMessageBytes > _limits.TargetMimeBytes - _reservedMimeBytes)
            {
                reason = $"Capacity stop: admitting this {mimeMessageBytes:N0} B MIME message would exceed the total MIME budget of {_limits.TargetMimeBytes:N0} B.";
                return false;
            }

            _reservedMimeBytes = checked(_reservedMimeBytes + mimeMessageBytes);
            _reservedAttempts++;
            reason = string.Empty;
            return true;
        }
    }
}
