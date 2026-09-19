using System.Text.Json;

namespace MailLoadTester;

public enum DiagnosticEvidenceStatus
{
    NotChecked,
    Observed,
    CheckFailed,
    NotEvaluated
}

/// <summary>Structured evidence projected from the existing transport diagnostics.</summary>
public sealed record AuthenticationTransportEvidence(
    string TargetHost,
    int Port,
    string SecurityMode,
    DiagnosticEvidenceStatus SmtpStatus,
    DiagnosticEvidenceStatus TlsStatus,
    string? TlsProtocol,
    DiagnosticEvidenceStatus MxStatus,
    int MxRecordCount,
    DiagnosticEvidenceStatus SpfStatus,
    bool HasSpf,
    DiagnosticEvidenceStatus DmarcStatus,
    bool HasDmarc,
    DiagnosticEvidenceStatus DkimStatus,
    string DkimReason,
    bool Authenticated,
    IReadOnlyList<string> AuthenticationMechanisms,
    IReadOnlyList<string> EvidenceSteps,
    string? Error)
{
    public static AuthenticationTransportEvidence FromReport(TransportDiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var dns = report.DnsPolicy;
        var spfStatus = dns is null
            ? DiagnosticEvidenceStatus.NotChecked
            : dns.SpfCheckFailed ? DiagnosticEvidenceStatus.CheckFailed
            : DiagnosticEvidenceStatus.Observed;
        var dmarcStatus = dns is null
            ? DiagnosticEvidenceStatus.NotChecked
            : dns.DmarcCheckFailed ? DiagnosticEvidenceStatus.CheckFailed
            : DiagnosticEvidenceStatus.Observed;
        var mxStatus = report.MxRecords is null
            ? DiagnosticEvidenceStatus.NotChecked
            : DiagnosticEvidenceStatus.Observed;
        var tlsStatus = report.Connected
            ? report.TlsProtocol is null
                ? DiagnosticEvidenceStatus.NotEvaluated
                : DiagnosticEvidenceStatus.Observed
            : DiagnosticEvidenceStatus.NotChecked;

        return new AuthenticationTransportEvidence(
            report.TargetHost,
            report.Port,
            report.SecurityMode,
            report.Connected ? DiagnosticEvidenceStatus.Observed : DiagnosticEvidenceStatus.CheckFailed,
            tlsStatus,
            report.TlsProtocol,
            mxStatus,
            report.MxRecords?.Count ?? 0,
            spfStatus,
            dns?.HasSpf ?? false,
            dmarcStatus,
            dns?.HasDmarc ?? false,
            DiagnosticEvidenceStatus.NotEvaluated,
            "DKIM requires a selector; the existing DNS policy checker intentionally does not guess one.",
            report.Authenticated,
            report.Capabilities?.AuthMechanisms ?? Array.Empty<string>(),
            report.Steps.ToArray(),
            report.Error);
    }

    public string ToJson()
        => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });
}

/// <summary>Evidence projection only; it never performs an additional network diagnostic.</summary>
public static class AuthenticationTransportEvidenceBuilder
{
    public static AuthenticationTransportEvidence FromReport(TransportDiagnosticReport report)
        => AuthenticationTransportEvidence.FromReport(report);
}
