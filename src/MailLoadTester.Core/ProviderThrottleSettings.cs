namespace MailLoadTester;

/// <summary>
/// Destination-provider throttle used by SmartPaceController.
/// The provider key is the recipient domain (for example, gmail.com).
/// Disabled by default; configure with environment variables so credentials and
/// scenario configuration stay out of source and RunReport.
///
/// LOAD2_PROVIDER_THROTTLE_ENABLED=true|false
/// LOAD2_PROVIDER_MAX_MESSAGES=20
/// LOAD2_PROVIDER_WINDOW_MINUTES=60
/// </summary>
public sealed record ProviderThrottleSettings(
    bool Enabled,
    int MaxMessages,
    int WindowMinutes)
{
    public static ProviderThrottleSettings FromEnvironment()
    {
        var enabled = ParseBool(Environment.GetEnvironmentVariable("LOAD2_PROVIDER_THROTTLE_ENABLED"));
        var maxMessages = ParseInt(Environment.GetEnvironmentVariable("LOAD2_PROVIDER_MAX_MESSAGES"), 20, 1, 100_000);
        var windowMinutes = ParseInt(Environment.GetEnvironmentVariable("LOAD2_PROVIDER_WINDOW_MINUTES"), 60, 1, 7 * 24 * 60);
        return new ProviderThrottleSettings(enabled, maxMessages, windowMinutes);
    }

    private static bool ParseBool(string? raw) =>
        bool.TryParse(raw, out var value) && value;

    private static int ParseInt(string? raw, int fallback, int min, int max) =>
        int.TryParse(raw, out var value) && value >= min && value <= max
            ? value
            : fallback;
}
