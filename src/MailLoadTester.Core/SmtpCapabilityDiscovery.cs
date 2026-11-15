using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailLoadTester;

/// <summary>
/// Read-only SMTP capability discovery for a configured submission endpoint or recipient MX.
/// It never sends a message and never authenticates.
/// </summary>
public sealed class SmtpCapabilityDiscoveryService
{
    public async Task<RecipientMxDiscovery> DiscoverRecipientMxAsync(
        string recipientAddress,
        int maxMxHosts = 3,
        int timeoutMs = 10_000,
        CancellationToken cancellationToken = default)
    {
        if (!Validation.IsValidEmail(recipientAddress))
            throw new ArgumentException("Recipient must be a valid email address.", nameof(recipientAddress));
        if (maxMxHosts is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(maxMxHosts));
        if (timeoutMs is < 1000 or > 60_000)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));

        var domain = DnsPolicyChecker.ExtractDomain(recipientAddress)
            ?? throw new ArgumentException("Recipient domain could not be determined.", nameof(recipientAddress));

        var mxRecords = await MxResolver.ResolveAsync(domain, cancellationToken).ConfigureAwait(false);
        var ordered = mxRecords.OrderBy(x => x.Preference).Take(maxMxHosts).ToArray();
        if (ordered.Length == 0)
        {
            return new RecipientMxDiscovery(
                domain,
                Array.Empty<MxRecord>(),
                Array.Empty<SmtpEndpointCapability>(),
                "No MX host could be resolved. This does not prove that the mailbox or domain does not exist.");
        }

        var probes = new List<SmtpEndpointCapability>(ordered.Length);
        foreach (var mx in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Direct-MX SMTP uses port 25 and opportunistic STARTTLS. No RCPT/MAIL/DATA
            // command is issued: this is only TCP + greeting + EHLO capability discovery.
            var result = await ProbeEndpointAsync(
                mx.Host, 25, SecureSocketOptions.StartTlsWhenAvailable, timeoutMs, cancellationToken)
                .ConfigureAwait(false);
            probes.Add(result);
        }

        return new RecipientMxDiscovery(
            domain,
            ordered,
            probes,
            "MX discovery is read-only. SIZE is a per-message maximum, not mailbox quota.");
    }

    public async Task<SmtpEndpointCapability> ProbeSubmissionEndpointAsync(
        string host,
        int port,
        SmtpSecurity security,
        int timeoutMs = 10_000,
        CancellationToken cancellationToken = default)
    {
        var socketOptions = security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurity.ImplicitTls => SecureSocketOptions.SslOnConnect,
            _ => throw new ArgumentOutOfRangeException(nameof(security))
        };

        return await ProbeEndpointAsync(host, port, socketOptions, timeoutMs, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<SmtpEndpointCapability> ProbeEndpointAsync(
        string host,
        int port,
        SecureSocketOptions socketOptions,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("SMTP host is required.", nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));
        if (timeoutMs is < 1000 or > 60_000)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));

        var stopwatch = Stopwatch.StartNew();
        string? certificateSubject = null;
        string? tlsProtocol = null;

        using var client = new SmtpClient { Timeout = timeoutMs };
        client.ServerCertificateValidationCallback = (_, certificate, _, errors) =>
        {
            certificateSubject = certificate?.Subject;
            return errors == SslPolicyErrors.None;
        };

        try
        {
            await client.ConnectAsync(host.Trim(), port, socketOptions, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();

            if (client.SslProtocol != SslProtocols.None)
                tlsProtocol = client.SslProtocol.ToString();

            var capabilities = TransportDiagnostics.FromClient(client);
            return new SmtpEndpointCapability(
                host.Trim(),
                port,
                socketOptions.ToString(),
                Connected: true,
                ConnectMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                MaxMessageBytes: capabilities.MaxSize,
                StartTlsAdvertised: capabilities.StartTls,
                AuthMechanisms: capabilities.AuthMechanisms,
                Pipelining: capabilities.Pipelining,
                EightBitMime: capabilities.EightBitMime,
                SmtpUtf8: capabilities.SmtpUtf8,
                Chunking: capabilities.Chunking,
                TlsProtocol: tlsProtocol,
                CertificateSubject: certificateSubject,
                Error: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new SmtpEndpointCapability(
                host.Trim(),
                port,
                socketOptions.ToString(),
                Connected: false,
                ConnectMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
                MaxMessageBytes: null,
                StartTlsAdvertised: false,
                AuthMechanisms: Array.Empty<string>(),
                Pipelining: false,
                EightBitMime: false,
                SmtpUtf8: false,
                Chunking: false,
                TlsProtocol: tlsProtocol,
                CertificateSubject: certificateSubject,
                Error: Validation.ExplainSmtpError(ex));
        }
        finally
        {
            if (client.IsConnected)
            {
                try { await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false); }
                catch { }
            }
        }
    }
}
