using System.Net;
using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class AuthenticationTransportEvidenceTests
{
    [Fact]
    public void FromReport_PreservesObservedTransportAndDnsEvidence()
    {
        var report = new TransportDiagnosticReport(
            "smtp.example.test", 587, "StartTls", true, true, 12.5,
            new EhloCapabilities
            {
                StartTls = true,
                AuthMechanisms = new List<string> { "PLAIN", "LOGIN" }
            },
            "Tls13", null,
            new DnsPolicyChecker.PolicyResult(
                "example.test", true, "v=spf1 -all", true, "v=DMARC1; p=reject", "ok"),
            new[] { new MxRecord("mx.example.test", 10, IPAddress.None) },
            new[] { "SMTP: connected", "SMTP: auth OK" },
            "ok", null);

        var evidence = AuthenticationTransportEvidence.FromReport(report);

        Assert.Equal(DiagnosticEvidenceStatus.Observed, evidence.SmtpStatus);
        Assert.Equal(DiagnosticEvidenceStatus.Observed, evidence.TlsStatus);
        Assert.Equal("Tls13", evidence.TlsProtocol);
        Assert.Equal(DiagnosticEvidenceStatus.Observed, evidence.MxStatus);
        Assert.Equal(1, evidence.MxRecordCount);
        Assert.Equal(DiagnosticEvidenceStatus.Observed, evidence.SpfStatus);
        Assert.True(evidence.HasSpf);
        Assert.Equal(DiagnosticEvidenceStatus.Observed, evidence.DmarcStatus);
        Assert.True(evidence.HasDmarc);
        Assert.Equal(DiagnosticEvidenceStatus.NotEvaluated, evidence.DkimStatus);
        Assert.True(evidence.Authenticated);
        Assert.Contains("PLAIN", evidence.AuthenticationMechanisms);
    }

    [Fact]
    public void FromReport_DoesNotClaimDkimWithoutSelector()
    {
        var report = new TransportDiagnosticReport(
            "smtp.example.test", 25, "None", false, false, 0,
            null, null, null, null, null, Array.Empty<string>(), "dry", null);

        var evidence = AuthenticationTransportEvidence.FromReport(report);

        Assert.Equal(DiagnosticEvidenceStatus.NotChecked, evidence.SpfStatus);
        Assert.Equal(DiagnosticEvidenceStatus.NotChecked, evidence.DmarcStatus);
        Assert.Equal(DiagnosticEvidenceStatus.NotEvaluated, evidence.DkimStatus);
        Assert.Contains("selector", evidence.DkimReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToJson_IsMachineReadableAndContainsNoCredentials()
    {
        var report = new TransportDiagnosticReport(
            "smtp.example.test", 587, "StartTls", true, false, 10,
            new EhloCapabilities { AuthMechanisms = new List<string> { "PLAIN" } },
            "Tls13", null, null, null, new[] { "SMTP: connected" }, "ok", null);

        var json = AuthenticationTransportEvidence.FromReport(report).ToJson();
        Assert.Contains("smtp.example.test", json);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", json, StringComparison.OrdinalIgnoreCase);
    }
}
