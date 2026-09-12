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
        var resultWithId = result.RunId == runId ? result : result with { RunId = runId };
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
        var normalized = report with { EndpointHealth = health };
        return JsonSerializer.Serialize(normalized, JsonOpts);
    }

    public static bool JsonContainsSecretMaterial(string json) =>
        json.Contains("\"password\"", StringComparison.OrdinalIgnoreCase)
        || json.Contains("\"proxyPassword\"", StringComparison.OrdinalIgnoreCase)
        || json.Contains("\"clientCertificatePassword\"", StringComparison.OrdinalIgnoreCase);
}
