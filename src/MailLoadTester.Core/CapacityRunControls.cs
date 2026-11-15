using MailKit.Net.Smtp;
using MimeKit;

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

        // Jitter can randomly shorten a send interval and violate a hard rate ceiling.
        // Disable only that speed-up source while the explicit ceiling is active.
        return options with
        {
            MessageCount = messageCount,
            IntervalMs = intervalMs,
            EnableJitter = limits.MaxMessagesPerMinute > 0 ? false : options.EnableJitter
        };
    }
}

/// <summary>
/// Conservative preflight estimate. The total covers the configured logical message
/// count without retries; retries add more SEND attempts and consume the runtime budget.
/// </summary>
public sealed record CapacityRunEstimate(
    long EstimatedMimeBytesPerMessage,
    long EstimatedMimeBytesForLogicalMessages,
    int LogicalMessageCount,
    bool IsComplete,
    string Note);

public static class CapacityRunPreview
{
    public static CapacityRunEstimate Estimate(
        IEnumerable<long> binaryAttachmentSizes,
        int requestedMessageCount,
        int maxMessageCount,
        int randomAttachmentCount = 0,
        long randomAttachmentSizeBytes = 0,
        bool hasUnaccountedContent = false)
    {
        ArgumentNullException.ThrowIfNull(binaryAttachmentSizes);
        if (requestedMessageCount < 1)
            throw new ArgumentOutOfRangeException(nameof(requestedMessageCount));
        if (maxMessageCount < 1)
            throw new ArgumentOutOfRangeException(nameof(maxMessageCount));
        if (randomAttachmentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(randomAttachmentCount));
        if (randomAttachmentSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(randomAttachmentSizeBytes));
        if (randomAttachmentCount > 0 && randomAttachmentSizeBytes == 0)
            throw new ArgumentException("Random attachment size must be positive when random attachments are included.", nameof(randomAttachmentSizeBytes));

        long estimatedPerMessage = 16 * 1024;
        foreach (var bytes in binaryAttachmentSizes)
            estimatedPerMessage = checked(estimatedPerMessage + MailboxCapacityMath.EstimateBase64EncodedBytes(bytes));

        for (var i = 0; i < randomAttachmentCount; i++)
            estimatedPerMessage = checked(estimatedPerMessage + MailboxCapacityMath.EstimateBase64EncodedBytes(randomAttachmentSizeBytes));

        var count = Math.Min(requestedMessageCount, maxMessageCount);
        var total = checked(estimatedPerMessage * count);
        var note = hasUnaccountedContent
            ? "Odhad je neúplný: šablona nebo generovaný obsah nebyly plně započteny. Nezahrnuje retry ani proměnlivé hlavičky."
            : "Konzervativní odhad bez retry; runtime limit používá skutečně serializovanou MIME velikost.";
        return new CapacityRunEstimate(estimatedPerMessage, total, count, !hasUnaccountedContent, note);
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
        => TryReserve(mimeMessageBytes, serverMaxMessageBytes: null, out reason);

    public bool TryReserve(long mimeMessageBytes, long? serverMaxMessageBytes, out string reason)
    {
        if (mimeMessageBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(mimeMessageBytes));
        if (serverMaxMessageBytes is < 0)
            throw new ArgumentOutOfRangeException(nameof(serverMaxMessageBytes));

        lock (_gate)
        {
            var effectiveMessageLimit = _limits.MaxMimeMessageBytes > 0
                ? _limits.MaxMimeMessageBytes
                : long.MaxValue;
            if (serverMaxMessageBytes is > 0)
                effectiveMessageLimit = Math.Min(effectiveMessageLimit, serverMaxMessageBytes.Value);

            if (effectiveMessageLimit != long.MaxValue && mimeMessageBytes > effectiveMessageLimit)
            {
                var source = serverMaxMessageBytes is > 0
                    && (_limits.MaxMimeMessageBytes == 0 || serverMaxMessageBytes.Value <= _limits.MaxMimeMessageBytes)
                        ? "server-advertised SMTP SIZE limit"
                        : "configured per-message ceiling";
                reason = $"Capacity stop: MIME message is {mimeMessageBytes:N0} B, above the {source} of {effectiveMessageLimit:N0} B.";
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

/// <summary>
/// Mirrors MailKit 4.17 SMTP transport preparation before measuring MIME bytes:
/// DOS newlines, final newline, negotiated UTF8/8BITMIME/BINARYMIME constraints,
/// and the transport's 998-octet line ceiling. This avoids measuring the
/// unprepared message when the SMTP transport will re-encode it before DATA.
/// </summary>
public static class CapacityRunMimeSizer
{
    public static long MeasurePreparedMimeBytes(SmtpClient client, MimeMessage message, FormatOptions? requestedFormat)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(message);

        var capabilities = client.Capabilities;
        var format = (requestedFormat ?? FormatOptions.Default).Clone();
        format.NewLineFormat = NewLineFormat.Dos;
        format.EnsureNewLine = true;

        if (format.International && !capabilities.HasFlag(SmtpCapabilities.UTF8))
            format.International = false;

        if (format.International && !capabilities.HasFlag(SmtpCapabilities.EightBitMime))
            throw new NotSupportedException("The SMTP server does not support the 8BITMIME extension required for international formatting.");

        var constraint = capabilities.HasFlag(SmtpCapabilities.BinaryMime)
            ? EncodingConstraint.None
            : capabilities.HasFlag(SmtpCapabilities.EightBitMime)
                ? EncodingConstraint.EightBit
                : EncodingConstraint.SevenBit;

        // MailKit deliberately avoids changing a signed message because doing so
        // would invalidate DKIM/ARC signatures. Keep that behavior identical.
        if (message.Headers.Contains(HeaderId.DomainKeySignature)
            || message.Headers.Contains(HeaderId.DkimSignature)
            || message.Headers.Contains(HeaderId.ArcSeal))
        {
            format.International = false;
        }
        else
        {
            message.Prepare(constraint, 998);
        }

        using var counter = new CountingStream();
        message.WriteTo(format, counter);
        return counter.BytesWritten;
    }
}
