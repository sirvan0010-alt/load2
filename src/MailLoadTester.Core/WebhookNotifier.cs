using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MailLoadTester;

public static class WebhookNotifier
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static async Task NotifyAsync(string url, MailTestResult result, CancellationToken ct)
    {
        var destination = await WebhookSecurityPolicy.ResolveSafeDestinationAsync(url, ct).ConfigureAwait(false);

        var json = JsonSerializer.Serialize(new
        {
            app = "MailLoadTester",
            version = AppVersion.Current,
            timestamp = DateTime.UtcNow,
            result.Requested,
            result.Sent,
            result.Failed,
            result.Cancelled,
            result.Elapsed,
            result.ThroughputPerSec,
            result.AvgLatencyMs,
            result.P95LatencyMs,
            result.Retries,
            result.Smtp4xx,
            result.Smtp5xx,
            result.LastError
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = Timeout,
            ConnectCallback = async (context, cancellationToken) =>
            {
                Exception? last = null;
                foreach (var address in destination.Addresses)
                {
                    try
                    {
                        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        try
                        {
                            await socket.ConnectAsync(
                                new IPEndPoint(address, context.DnsEndPoint.Port),
                                cancellationToken).ConfigureAwait(false);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            socket.Dispose();
                            throw;
                        }
                    }
                    catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                    {
                        last = ex;
                        if (ex is OperationCanceledException)
                            throw;
                    }
                }

                throw new HttpRequestException("Webhook connection failed.", last);
            }
        };
        using var client = new HttpClient(handler) { Timeout = Timeout };

        try
        {
            using var response = await client.PostAsync(destination.Uri, content, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Best effort — don't fail the test if webhook fails */ }
    }
}

internal sealed record WebhookDestination(Uri Uri, IReadOnlyList<IPAddress> Addresses);

internal static class WebhookSecurityPolicy
{
    public static async Task<WebhookDestination> ResolveSafeDestinationAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Webhook URL musí být platná absolutní URL.", nameof(url));

        if (uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.IsLoopback)
            throw new ArgumentException(
                "Webhook URL musí používat HTTP(S) a nesmí mířit na loopback ani obsahovat přihlašovací údaje.",
                nameof(url));

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.DnsSafeHost, out var literal))
        {
            addresses = new[] { literal };
        }
        else
        {
            try
            {
                addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                throw new ArgumentException(
                    "Webhook host se nepodařilo přeložit na IP adresu.", nameof(url), ex);
            }
        }

        addresses = addresses.Distinct().ToArray();
        if (addresses.Length == 0 || addresses.Any(IsNonPublicAddress))
            throw new ArgumentException(
                "Webhook nesmí mířit na privátní, loopback, link-local, multicast nebo jinou neveřejnou IP adresu.",
                nameof(url));

        return new WebhookDestination(uri, addresses);
    }

    private static bool IsNonPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any))
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 0 ||
                   b[0] == 10 ||
                   b[0] == 127 ||
                   (b[0] == 100 && b[1] is >= 64 and <= 127) ||
                   (b[0] == 169 && b[1] == 254) ||
                   (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                   (b[0] == 192 && b[1] == 168) ||
                   (b[0] == 192 && b[1] == 0 && b[2] == 0) ||
                   (b[0] == 198 && b[1] is 18 or 19) ||
                   b[0] >= 224;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6Multicast ||
                   address.IsIPv6SiteLocal ||
                   b[0] == 0 ||
                   (b[0] & 0xFE) == 0xFC;
        }

        return true;
    }
}
