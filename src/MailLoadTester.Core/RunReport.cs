using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailLoadTester;

/// <summary>
/// FEAT-REPORT: machine-readable, secret-free summary of one run.
/// Built on top of <see cref="MailTestResult"/> — not a parallel metrics system.
/// </summary>
public sealed record RunReport(
    string RunId,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    RunReportOptions Options,
    MailTestResult Result,
    IReadOnlyList<EndpointHealthSnapshot> EndpointHealth,
    int LedgerAccepted,
    int LedgerFailed);

/// <summary>Sanitized options: no passwords, cert secrets, or proxy secrets.</summary>
public sealed record RunReportOptions(
    string SmtpHost,
    int Port,
    string Security,
    bool UseAuthentication,
    string Username,
    bool DryRun,
    bool TestMode,
    bool Unauthorized,
    int MessageCount,
    int MaxConcurrency,
    int IntervalMs,
    int MaxRetries,
    bool DirectMxDelivery,
    bool UseSocks5Proxy,
    bool UseAdaptiveConcurrency,
    bool UseCircuitBreaker,
    bool AutoRestartOnFailure,
    int RecipientCount);

public static class RunReportBuilder
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static RunReportOptions FromOptions(MailTestOptions o) => new(
        SmtpHost: o.SmtpHost,
        Port: o.Port,
        Security: o.Security.ToString(),
        UseAuthentication: o.UseAuthentication,
        Username: o.UseAuthentication ? (o.Username ?? "") : "",
        DryRun: o.DryRun,
        TestMode: o.TestMode,
        Unauthorized: o.Unauthorized,
        MessageCount: o.MessageCount,
        MaxConcurrency: o.MaxConcurrency,
        IntervalMs: o.IntervalMs,
        MaxRetries: o.MaxRetries,
        DirectMxDelivery: o.DirectMxDelivery,
        UseSocks5Proxy: o.UseSocks5Proxy || !string.IsNullOrWhiteSpace(o.ProxyList),
        UseAdaptiveConcurrency: o.UseAdaptiveConcurrency,
        UseCircuitBreaker: o.UseCircuitBreaker,
        AutoRestartOnFailure: o.AutoRestartOnFailure,
        RecipientCount: o.Recipients?.Count ?? 0);

    public static RunReport Create(
        string runId,
        DateTimeOffset startedUtc,
        DateTimeOffset finishedUtc,
        MailTestOptions options,
        MailTestResult result,
        IReadOnlyList<EndpointHealthSnapshot>? health = null,
        int? ledgerAccepted = null,
        int? ledgerFailed = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
            runId = Guid.NewGuid().ToString("N");
        health ??= result.EndpointHealth ?? Array.Empty<EndpointHealthSnapshot>();
        var resultWithId = result with
        {
            RunId = runId,
            LastError = RedactKnownSecrets(
                result.LastError,
                options.Password,
                options.ProxyPassword,
                options.ClientCertificatePassword)
        };
        return new RunReport(
            RunId: runId,
            StartedUtc: startedUtc,
            FinishedUtc: finishedUtc,
            Options: FromOptions(options),
            Result: resultWithId,
            EndpointHealth: health,
            LedgerAccepted: ledgerAccepted ?? result.Sent,
            LedgerFailed: ledgerFailed ?? result.Failed);
    }

    public static string ToJson(RunReport report)
    {
        var health = report.EndpointHealth
            .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var resultHealth = report.Result.EndpointHealth?
            .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedResult = report.Result with { EndpointHealth = resultHealth };
        var normalized = report with
        {
            EndpointHealth = health,
            Result = normalizedResult
        };
        return JsonSerializer.Serialize(normalized, JsonOpts);
    }

    private static string RedactKnownSecrets(string? text, params string?[] secrets)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        foreach (var secret in secrets.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal))
        {
            text = text.Replace(secret!, "[REDACTED]", StringComparison.Ordinal);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(secret!));
            if (!string.IsNullOrEmpty(base64))
                text = text.Replace(base64, "[REDACTED]", StringComparison.Ordinal);
        }

        return text;
    }

    public static bool JsonContainsSecretMaterial(string json) =>
        json.Contains("\"password\"", StringComparison.OrdinalIgnoreCase)
        || json.Contains("\"proxyPassword\"", StringComparison.OrdinalIgnoreCase)
        || json.Contains("\"clientCertificatePassword\"", StringComparison.OrdinalIgnoreCase);
}
