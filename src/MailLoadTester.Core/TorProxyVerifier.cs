using System.Net.Security;
using System.Text;
using System.Text.Json;

namespace MailLoadTester;

/// <summary>
/// Ověří skutečný Tor exit přes stejný SOCKS5 klient, který používá SMTP.
/// Pouhá dostupnost lokálního portu nestačí k označení Toru jako aktivního.
/// </summary>
public static class TorProxyVerifier
{
    public sealed record Result(bool IsTor, string? ExitIp, string Detail);

    public static async Task<Result> VerifyAsync(ProxyEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        if (endpoint.Type != ProxyType.Socks5)
            return new Result(false, null, "Není nakonfigurována SOCKS5 proxy.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));

        try
        {
            var proxy = ProxyClientFactory.Create(endpoint)
                ?? throw new InvalidOperationException("SOCKS5 proxy klient se nepodařilo vytvořit.");

            await using var network = await proxy.ConnectAsync(
                "check.torproject.org", 443, 10_000, timeout.Token).ConfigureAwait(false);

            await using var tls = new SslStream(network, leaveInnerStreamOpen: false);
            await tls.AuthenticateAsClientAsync(
                new System.Net.Security.SslClientAuthenticationOptions { TargetHost = "check.torproject.org" },
                timeout.Token).ConfigureAwait(false);

            var request = "GET /api/ip HTTP/1.1\r\nHost: check.torproject.org\r\nUser-Agent: MailLoadTester-TorCheck/1.0\r\nAccept: application/json\r\nConnection: close\r\n\r\n";
            var requestBytes = Encoding.ASCII.GetBytes(request);
            await tls.WriteAsync(requestBytes, timeout.Token).ConfigureAwait(false);
            await tls.FlushAsync(timeout.Token).ConfigureAwait(false);

            using var reader = new StreamReader(tls, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var response = await reader.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
            var separator = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (separator < 0)
                return new Result(false, null, "Tor kontrolní server vrátil neplatnou HTTP odpověď.");

            var headers = response[..separator];
            var body = response[(separator + 4)..];
            if (!headers.StartsWith("HTTP/1.1 200", StringComparison.OrdinalIgnoreCase)
                && !headers.StartsWith("HTTP/1.0 200", StringComparison.OrdinalIgnoreCase))
                return new Result(false, null, $"Tor kontrolní server odpověděl: {headers.Split('\n')[0].Trim()}");

            if (headers.Contains("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase))
                body = DecodeChunkedBody(body);

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var isTor = root.TryGetProperty("IsTor", out var torValue) && torValue.ValueKind == JsonValueKind.True;
            var ip = root.TryGetProperty("IP", out var ipValue) ? ipValue.GetString() : null;

            return isTor
                ? new Result(true, ip, "Tor exit ověřen kontrolním serverem Tor Project.")
                : new Result(false, ip, "SOCKS5 spojení funguje, ale kontrolní server nepotvrdil Tor exit.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Result(false, null, "Ověření Toru překročilo časový limit 12 sekund.");
        }
        catch (Exception ex)
        {
            return new Result(false, null, $"Tor se nepodařilo ověřit: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string DecodeChunkedBody(string input)
    {
        var output = new StringBuilder();
        var position = 0;
        while (position < input.Length)
        {
            var lineEnd = input.IndexOf("\r\n", position, StringComparison.Ordinal);
            if (lineEnd < 0) break;
            var sizeText = input[position..lineEnd].Split(';', 2)[0].Trim();
            if (!int.TryParse(sizeText, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var size) || size <= 0)
                break;
            position = lineEnd + 2;
            if (position + size > input.Length) break;
            output.Append(input, position, size);
            position += size + 2;
        }
        return output.ToString();
    }
}
