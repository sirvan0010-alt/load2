namespace MailLoadTester;

/// <summary>SMTP capabilities advertised by one explicitly selected endpoint.</summary>
public sealed record SmtpEndpointCapability(
    string Host,
    int Port,
    string SecurityMode,
    bool Connected,
    double ConnectMilliseconds,
    long? MaxMessageBytes,
    bool StartTlsAdvertised,
    IReadOnlyList<string> AuthMechanisms,
    bool Pipelining,
    bool EightBitMime,
    bool SmtpUtf8,
    bool Chunking,
    string? TlsProtocol,
    string? CertificateSubject,
    string? Error);

/// <summary>Read-only discovery results for the recipient domain's MX hosts.</summary>
public sealed record RecipientMxDiscovery(
    string RecipientDomain,
    IReadOnlyList<MxRecord> MxRecords,
    IReadOnlyList<SmtpEndpointCapability> Probes,
    string Note)
{
    /// <summary>Lowest advertised maximum across reachable probed MX hosts, if all report one.</summary>
    public long? ConservativeAdvertisedMessageLimitBytes
    {
        get
        {
            var reachable = Probes.Where(x => x.Connected).ToArray();
            if (reachable.Length == 0 || reachable.Any(x => !x.MaxMessageBytes.HasValue))
                return null;
            return reachable.Min(x => x.MaxMessageBytes!.Value);
        }
    }
}

/// <summary>
/// MIME sizing helpers. These are estimates for Base64 attachments, not mailbox quota.
/// They deliberately reserve caller-specified bytes for headers, body and MIME boundaries.
/// </summary>
public static class MailboxCapacityMath
{
    public static long EstimateBase64EncodedBytes(long binaryBytes)
    {
        if (binaryBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(binaryBytes));

        // Base64 expands 3 input bytes to 4 output characters. MIME line wrapping
        // adds CRLF after each 76 characters (except possibly the last line).
        var base64Characters = checked(((binaryBytes + 2) / 3) * 4);
        var lineBreaks = base64Characters == 0 ? 0 : (base64Characters - 1) / 76;
        return checked(base64Characters + lineBreaks * 2);
    }

    public static long EstimateMimeMessageBytes(long binaryAttachmentBytes, long nonAttachmentOverheadBytes = 16 * 1024)
    {
        if (nonAttachmentOverheadBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(nonAttachmentOverheadBytes));
        return checked(EstimateBase64EncodedBytes(binaryAttachmentBytes) + nonAttachmentOverheadBytes);
    }

    public static long? EstimateMaxBinaryAttachmentBytes(long? serverMaxMessageBytes, long nonAttachmentOverheadBytes = 16 * 1024)
    {
        if (!serverMaxMessageBytes.HasValue)
            return null;
        if (serverMaxMessageBytes.Value <= nonAttachmentOverheadBytes || nonAttachmentOverheadBytes < 0)
            return 0;

        var available = serverMaxMessageBytes.Value - nonAttachmentOverheadBytes;
        // Invert Base64 + CRLF wrapping conservatively, then correct for exact rounding.
        var candidate = Math.Max(0, (long)Math.Floor(available * 0.73d));
        while (candidate > 0 && EstimateBase64EncodedBytes(candidate) > available)
            candidate--;
        while (candidate < long.MaxValue && EstimateBase64EncodedBytes(candidate + 1) <= available)
            candidate++;
        return candidate;
    }
}
