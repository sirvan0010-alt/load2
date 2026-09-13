using System.Diagnostics;
using System.Security.Authentication;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailLoadTester;

/// <summary>
/// What sections to run. All diagnostics require an explicit target in options;
/// nothing scans the open internet or probes third-party hosts beyond SmtpHost / From domain.
/// </summary>
public sealed record TransportDiagnosticOptions(
    bool CheckDnsPolicy = true,
    bool CheckMx = false,
    bool CheckSmtp = true,
    bool TryAuthenticate = false,
    bool DryRun = false);

/// <summary>Machine-readable pre-flight report (no passwords).</summary>
public sealed record TransportDiagnosticReport(
    string TargetHost,
    int Port,
    string SecurityMode,
    bool Connected,
    bool Authenticated,
    double ConnectMs,
    EhloCapabilities? Capabilities,
    string? TlsProtocol,
    string? CertificateSubject,
    DnsPolicyChecker.PolicyResult? DnsPolicy,
    IReadOnlyList<MxRecord>? MxRecords,
    IReadOnlyList<string> Steps,
    string Summary,
    string? Error);

/// <summary>
/// DNS / TLS / SMTP capability diagnostics layered on existing helpers.
/// Does not send mail and does not alter the persistent-session send path.
/// </summary>
public static class TransportDiagnostics
{
    public static EhloCapabilities FromClient(SmtpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var caps = client.Capabilities;
        return new EhloCapabilities
        {
            StartTls = caps.HasFlag(SmtpCapabilities.StartTLS),
            AuthMechanisms = client.AuthenticationMechanisms.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList(),
            MaxSize = client.MaxSize > 0 ? client.MaxSize : null,
            Pipelining = caps.HasFlag(SmtpCapabilities.Pipelining),
            EightBitMime = caps.HasFlag(SmtpCapabilities.EightBitMime),
            SmtpUtf8 = caps.HasFlag(SmtpCapabilities.UTF8),
            Chunking = caps.HasFlag(SmtpCapabilities.Chunking),
            RawLines = Array.Empty<string>()
        };
    }

    public static async Task<TransportDiagnosticReport> RunAsync(
        MailTestOptions options,
        TransportDiagnosticOptions? diag = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        diag ??= new TransportDiagnosticOptions();
        var steps = new List<string>();
        var dry = diag.DryRun || options.DryRun;

        if (dry)
        {
            steps.Add("DryRun: skipped network I/O");
            return new TransportDiagnosticReport(
                TargetHost: options.SmtpHost,
                Port: options.Port,
                SecurityMode: options.Security.ToString(),
                Connected: false,
                Authenticated: false,
                ConnectMs: 0,
                Capabilities: null,
                TlsProtocol: null,
                CertificateSubject: null,
                DnsPolicy: null,
                MxRecords: null,
                Steps: steps,
                Summary: $"DRY-RUN diagnostics for {options.SmtpHost}:{options.Port} (Security={options.Security})",
                Error: null);
        }

        DnsPolicyChecker.PolicyResult? dns = null;
        IReadOnlyList<MxRecord>? mx = null;
        string? error = null;
        var connected = false;
        var authenticated = false;
        double connectMs = 0;
        EhloCapabilities? caps = null;
        string? tlsProtocol = null;
        string? certSubject = null;

        if (diag.CheckDnsPolicy)
        {
            ct.ThrowIfCancellationRequested();
            var domain = DnsPolicyChecker.ExtractDomain(options.From);
            if (!string.IsNullOrEmpty(domain))
            {
                steps.Add($"DNS policy: checking SPF/DMARC for {domain}");
                try
                {
                    dns = await DnsPolicyChecker.CheckAsync(domain, ct).ConfigureAwait(false);
                    steps.Add($"DNS policy: SPF={dns.HasSpf}, DMARC={dns.HasDmarc}");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    steps.Add($"DNS policy failed: {ex.GetType().Name}");
                    error ??= ex.Message;
                }
            }
            else
            {
                steps.Add("DNS policy: skipped (From has no domain)");
            }
        }

        if (diag.CheckMx)
        {
            ct.ThrowIfCancellationRequested();
            var domain = DnsPolicyChecker.ExtractDomain(options.From) ?? options.SmtpHost;
            steps.Add($"MX: resolving {domain}");
            try
            {
                mx = await MxResolver.ResolveAsync(domain, ct).ConfigureAwait(false);
                steps.Add($"MX: {mx.Count} record(s)");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                steps.Add($"MX failed: {ex.GetType().Name}");
                error ??= ex.Message;
            }
        }

        if (diag.CheckSmtp && !string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            ct.ThrowIfCancellationRequested();
            steps.Add($"SMTP: connect {options.SmtpHost}:{options.Port} ({options.Security})");
            var sw = Stopwatch.StartNew();
            try
            {
                using var client = new SmtpClient { Timeout = Math.Max(1000, options.ConnectTimeoutMs) };
                if (options.IgnoreCertificateErrors)
                    client.ServerCertificateValidationCallback = (_, _, _, _) => true;

                using var cert = ClientCertificateHelper.Load(options.ClientCertificatePath, options.ClientCertificatePassword);
                if (cert is not null)
                    client.ClientCertificates.Add(cert);

                if (!string.IsNullOrWhiteSpace(options.ProxyList))
                {
                    var list = ProxyClientFactory.ParseList(options.ProxyList);
                    if (list.Count > 0)
                    {
                        var proxy = ProxyClientFactory.Create(list[0]);
                        if (proxy != null)
                            client.ProxyClient = proxy;
                    }
                }
                else if (options.UseSocks5Proxy && !string.IsNullOrWhiteSpace(options.ProxyHost))
                {
                    var ep = new ProxyEndpoint(
                        ProxyType.Socks5, options.ProxyHost, options.ProxyPort,
                        string.IsNullOrEmpty(options.ProxyUsername) ? null : options.ProxyUsername,
                        string.IsNullOrEmpty(options.ProxyPassword) ? null : options.ProxyPassword);
                    var proxy = ProxyClientFactory.Create(ep);
                    if (proxy != null)
                        client.ProxyClient = proxy;
                }

                var socket = SmtpConnectivityTester.ToSocketOptions(options.Security);
                await client.ConnectAsync(options.SmtpHost, options.Port, socket, ct).ConfigureAwait(false);
                sw.Stop();
                connectMs = sw.Elapsed.TotalMilliseconds;
                connected = client.IsConnected;
                caps = FromClient(client);
                steps.Add($"SMTP: connected in {connectMs:F0} ms; AUTH mechs={caps.AuthMechanisms.Count}");

                try
                {
                    var ssl = client.SslProtocol;
                    if (ssl != SslProtocols.None)
                        tlsProtocol = ssl.ToString();
                }
                catch { }

                if (diag.TryAuthenticate && options.UseAuthentication)
                {
                    steps.Add("SMTP: authenticating (password not logged)");
                    if (options.AuthMethod == SmtpAuthMethod.Auto)
                        await client.AuthenticateAsync(options.Username, options.Password, ct).ConfigureAwait(false);
                    else
                    {
                        var sasl = AuthMethodHelper.CreateSasl(options.AuthMethod, options.Username, options.Password);
                        await client.AuthenticateAsync(sasl, ct).ConfigureAwait(false);
                    }
                    authenticated = client.IsAuthenticated;
                    steps.Add(authenticated ? "SMTP: auth OK" : "SMTP: auth finished without IsAuthenticated");
                }

                try { await client.DisconnectAsync(true, ct).ConfigureAwait(false); }
                catch { }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                sw.Stop();
                connectMs = sw.Elapsed.TotalMilliseconds;
                steps.Add($"SMTP failed: {ex.GetType().Name}");
                error = Validation.ExplainSmtpError(ex);
            }
        }
        else if (diag.CheckSmtp)
        {
            steps.Add("SMTP: skipped (empty SmtpHost)");
        }

        var summary = BuildSummary(options, connected, authenticated, caps, dns, mx, error);
        return new TransportDiagnosticReport(
            TargetHost: options.SmtpHost,
            Port: options.Port,
            SecurityMode: options.Security.ToString(),
            Connected: connected,
            Authenticated: authenticated,
            ConnectMs: connectMs,
            Capabilities: caps,
            TlsProtocol: tlsProtocol,
            CertificateSubject: certSubject,
            DnsPolicy: dns,
            MxRecords: mx,
            Steps: steps,
            Summary: summary,
            Error: error);
    }

    static string BuildSummary(
        MailTestOptions o,
        bool connected,
        bool authenticated,
        EhloCapabilities? caps,
        DnsPolicyChecker.PolicyResult? dns,
        IReadOnlyList<MxRecord>? mx,
        string? error)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Target: {o.SmtpHost}:{o.Port} ({o.Security})");
        if (dns != null)
            sb.AppendLine($"DNS: SPF={(dns.HasSpf ? "yes" : "no")}, DMARC={(dns.HasDmarc ? "yes" : "no")}");
        if (mx != null)
            sb.AppendLine($"MX: {mx.Count} record(s)");
        sb.AppendLine($"SMTP connected: {(connected ? "yes" : "no")}");
        if (caps != null)
        {
            sb.AppendLine($"STARTTLS capability: {(caps.StartTls ? "yes" : "no")}");
            sb.AppendLine($"AUTH: {(caps.AuthMechanisms.Count > 0 ? string.Join(", ", caps.AuthMechanisms) : "none advertised")}");
            sb.AppendLine($"PIPELINING: {(caps.Pipelining ? "yes" : "no")}");
        }
        if (authenticated)
            sb.AppendLine("Authenticated: yes");
        if (!string.IsNullOrEmpty(error))
            sb.AppendLine($"Error: {error}");
        return sb.ToString().TrimEnd();
    }
}
