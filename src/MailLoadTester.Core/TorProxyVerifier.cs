using System.Net;
using System.IO;
using System.Net.Http;
using MailKit.Net.Proxy;
using System.Net.Sockets;
using System.Text.Json;
using MailKit.Net.Proxy;

namespace MailLoadTester;

public sealed record TorProxyVerificationResult(bool IsTor, string? ExitIp, string Message);

/// <summary>
/// Verifies a local SOCKS5 endpoint against the Tor Project check API.
/// The HTTP handler hands the target hostname to SOCKS5, so the target hostname
/// is resolved by the proxy rather than the workstation's DNS resolver.
/// </summary>
public static class TorProxyVerifier
{
    private static readonly Uri CheckUri = new("https://check.torproject.org/api/ip");

    public static async Task<TorProxyVerificationResult> VerifyAsync(
        MailTestOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.RequireTor)
            throw new ArgumentException("Tor verification requires RequireTor=true.", nameof(options));

        if (!options.UseSocks5Proxy)
            throw new ArgumentException("Režim Tor vyžaduje zapnutou SOCKS5 proxy.", nameof(options));

        var validation = ValidateEndpoint(new ProxyEndpoint(
            ProxyType.Socks5,
            options.ProxyHost,
            options.ProxyPort,
            string.IsNullOrWhiteSpace(options.ProxyUsername) ? null : options.ProxyUsername,
            string.IsNullOrWhiteSpace(options.ProxyPassword) ? null : options.ProxyPassword));

        if (!string.IsNullOrWhiteSpace(options.ProxyList)
            || !string.IsNullOrWhiteSpace(options.Ipv4Rotation)
            || !string.IsNullOrWhiteSpace(options.Ipv6Prefix)
            || options.DirectMxDelivery
            || options.CheckRblBeforeStart)
            throw new InvalidOperationException("Konfigurace obsahuje funkci, která by v režimu Tor mohla obejít SOCKS5 tunel.");

        return await VerifyAsync(validation, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<TorProxyVerificationResult> VerifyAsync(
        ProxyEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        endpoint = ValidateEndpoint(endpoint);
        var proxy = ProxyClientFactory.Create(endpoint)
            ?? throw new InvalidOperationException("Nepodařilo se vytvořit SOCKS5 klienta.");

        using var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = async (context, token) =>
            {
                // Pass the hostname unchanged. MailKit's SOCKS5 client sends it
                // in the SOCKS request; local DNS is not used for the check host.
                return await ConnectThroughProxyAsync(proxy, context.DnsEndPoint, token).ConfigureAwait(false);
            }
        };

        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        using var request = new HttpRequestMessage(HttpMethod.Get, CheckUri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        using var response = await http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Tor check API vrátilo HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseCheckResponse(json);
    }

    internal static ValueTask<Stream> ConnectThroughProxyAsync(
        IProxyClient proxy,
        DnsEndPoint destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentNullException.ThrowIfNull(destination);
        return new ValueTask<Stream>(
            proxy.ConnectAsync(destination.Host, destination.Port, cancellationToken));
    }

    internal static ProxyEndpoint ValidateEndpoint(ProxyEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (endpoint.Type != ProxyType.Socks5)
            throw new ArgumentException("Tor vyžaduje SOCKS5 proxy.", nameof(endpoint));
        if (!IPAddress.TryParse(endpoint.Host, out var address) || !IPAddress.IsLoopback(address))
            throw new ArgumentException("Tor režim přijímá pouze lokální SOCKS5 adresu 127.0.0.1 nebo ::1.", nameof(endpoint));
        if (endpoint.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(endpoint), "SOCKS5 port musí být 1–65535.");
        return endpoint;
    }

    internal static TorProxyVerificationResult ParseCheckResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var isTor = root.TryGetProperty("IsTor", out var torElement)
                && torElement.ValueKind == JsonValueKind.True;
            var ip = root.TryGetProperty("IP", out var ipElement)
                ? ipElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out _))
                throw new InvalidDataException("Tor check API nevrátilo platnou výstupní IP adresu.");

            return isTor
                ? new TorProxyVerificationResult(true, ip, "Výstupní IP je potvrzena jako Tor exit node.")
                : new TorProxyVerificationResult(false, ip, "SOCKS5 spojení funguje, ale výstupní IP nebyla potvrzena jako Tor exit node.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Tor check API vrátilo neplatný JSON.", ex);
        }
    }
}
