namespace MailLoadTester;

/// <summary>
/// SEC-003/SEC-004: explicit acknowledgement required before unrestricted network send.
/// Default-safe path is Test mode (domain allow-list) or DryRun (no SMTP).
/// CLI: pass <c>--unauthorized</c> to acknowledge authorized load testing outside Test mode.
/// </summary>
public static class AuthorizationGate
{
    public const string UnauthorizedFlag = "--unauthorized";

    public static bool HasUnauthorizedFlag(IEnumerable<string>? args)
    {
        if (args is null) return false;
        foreach (var a in args)
        {
            if (string.Equals(a, UnauthorizedFlag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Throws if options would perform a real network send without Test mode or explicit ack.
    /// DryRun always allowed (no SMTP).
    /// </summary>
    public static void EnsureSendAuthorized(MailTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.DryRun) return;
        if (options.TestMode) return;
        if (options.Unauthorized) return;

        throw new InvalidOperationException(
            "Odesílání mimo Test mode vyžaduje explicitní potvrzení: CLI přepínač --unauthorized " +
            "nebo MailTestOptions.Unauthorized = true (autorizovaný load test na vlastní infrastruktuře).");
    }
}
