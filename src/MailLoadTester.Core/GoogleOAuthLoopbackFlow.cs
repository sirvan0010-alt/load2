using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace MailLoadTester;

/// <summary>
/// Completes the installed-app OAuth flow using a short-lived HTTP listener bound
/// exclusively to IPv4 loopback. No client secret is used or accepted.
/// </summary>
public sealed class GoogleOAuthLoopbackFlow
{
    private const int MaxRequestHeaderBytes = 8192;

    public async Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(
        GoogleOAuthClient client,
        Func<Uri, CancellationToken, Task> openBrowser,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(openBrowser);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(1);

        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var redirectUri = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute);
        var pkce = GoogleOAuthClient.CreatePkceChallenge();
        var authorizationUri = client.BuildAuthorizationUri(redirectUri, pkce);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stage = "příprava callbacku";
        try
        {
            // Begin accepting before launching the browser. This avoids a race with
            // browser launchers and also makes the callback flow deterministic in tests.
            var acceptTask = listener.AcceptTcpClientAsync(timeoutCts.Token).AsTask();
            try
            {
                stage = "otevření systémového prohlížeče";
                await openBrowser(authorizationUri, timeoutCts.Token).ConfigureAwait(false);
            }
            catch
            {
                timeoutCts.Cancel();
                try { await acceptTask.ConfigureAwait(false); }
                catch { /* preserve the browser-launch exception */ }
                throw;
            }

            stage = "čekání na lokální OAuth callback";
            using var tcpClient = await acceptTask.ConfigureAwait(false);
            tcpClient.NoDelay = true;
            await using var stream = tcpClient.GetStream();

            stage = "čtení lokální OAuth odpovědi";
            var request = await ReadRequestTargetAsync(stream, timeoutCts.Token).ConfigureAwait(false);
            GoogleOAuthCallback callback;
            try
            {
                callback = ParseCallback(request, pkce.State);
            }
            catch (InvalidOperationException ex)
            {
                await WriteBrowserResponseAsync(stream, HttpStatusCode.BadRequest,
                    "Přihlášení se nepodařilo dokončit. Vraťte se do MailLoadTester a zkuste to znovu.",
                    timeoutCts.Token).ConfigureAwait(false);
                throw new GoogleOAuthAuthorizationException(ex.Message);
            }

            stage = "výměna autorizačního kódu";
            var tokens = await client.ExchangeCodeAsync(
                callback.Code, redirectUri, pkce.CodeVerifier, timeoutCts.Token).ConfigureAwait(false);
            stage = "ověření Google účtu";
            var user = await client.GetUserAsync(tokens.AccessToken, timeoutCts.Token).ConfigureAwait(false);

            stage = "odpověď do prohlížeče";
            await WriteBrowserResponseAsync(stream, HttpStatusCode.OK,
                "Přihlášení bylo dokončeno. Můžete zavřít toto okno a vrátit se do MailLoadTester.",
                timeoutCts.Token).ConfigureAwait(false);

            return new GoogleOAuthAuthorizationResult(user, tokens);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Přihlášení přes Google vypršelo během fáze: " + stage + ". Zkuste to prosím znovu.");
        }
        finally
        {
            listener.Stop();
        }
    }

    public static GoogleOAuthCallback ParseCallback(string requestTarget, string expectedState)
    {
        if (string.IsNullOrWhiteSpace(requestTarget)
            || !requestTarget.StartsWith("/", StringComparison.Ordinal)
            || requestTarget.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException("Google vrátil neplatnou lokální OAuth odpověď.");

        if (string.IsNullOrWhiteSpace(expectedState))
            throw new ArgumentException("Očekávaný OAuth state nesmí být prázdný.", nameof(expectedState));

        if (!Uri.TryCreate("http://127.0.0.1" + requestTarget, UriKind.Absolute, out var callbackUri))
            throw new InvalidOperationException("Google vrátil neplatnou OAuth odpověď.");

        if (!callbackUri.AbsolutePath.Equals("/", StringComparison.Ordinal))
            throw new InvalidOperationException("Google vrátil neočekávanou cestu lokální OAuth odpovědi.");

        var parameters = ParseQuery(callbackUri.Query);
        if (!parameters.TryGetValue("state", out var returnedState)
            || !FixedTimeEquals(returnedState, expectedState))
            throw new InvalidOperationException("OAuth state nesouhlasí. Přihlášení bylo z bezpečnostních důvodů odmítnuto.");

        if (parameters.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
        {
            var safeError = error.Length > 100 ? error[..100] : error;
            throw new InvalidOperationException("Google odmítl přihlášení: " + safeError);
        }

        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Google nevrátil autorizační kód.");

        return new GoogleOAuthCallback(code);
    }

    private static async Task<string> ReadRequestTargetAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>(512);
        var one = new byte[1];
        var marker = new byte[] { 13, 10, 13, 10 };

        while (bytes.Count < MaxRequestHeaderBytes)
        {
            var read = await stream.ReadAsync(one.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new InvalidOperationException("Lokální OAuth spojení bylo předčasně ukončeno.");

            bytes.Add(one[0]);
            if (bytes.Count >= 4
                && bytes[^4] == marker[0]
                && bytes[^3] == marker[1]
                && bytes[^2] == marker[2]
                && bytes[^1] == marker[3])
                break;
        }

        if (bytes.Count >= MaxRequestHeaderBytes)
            throw new InvalidOperationException("Lokální OAuth požadavek překročil povolenou délku.");

        var header = Encoding.ASCII.GetString(bytes.ToArray());
        var requestLine = header.Split("\r\n", 2, StringSplitOptions.None)[0];
        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !parts[0].Equals("GET", StringComparison.Ordinal))
            throw new InvalidOperationException("Lokální OAuth požadavek nemá očekávaný formát GET.");

        return parts[1];
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=');
            var key = Decode(separator < 0 ? item : item[..separator]);
            var value = Decode(separator < 0 ? string.Empty : item[(separator + 1)..]);
            if (!result.TryAdd(key, value))
                throw new InvalidOperationException("Google vrátil duplicitní OAuth parametr.");
        }

        return result;
    }

    private static string Decode(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));

    private static bool FixedTimeEquals(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return actualBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static async Task WriteBrowserResponseAsync(
        NetworkStream stream,
        HttpStatusCode status,
        string message,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(
            "<!doctype html><html lang=\"cs\"><meta charset=\"utf-8\"><title>MailLoadTester</title>" +
            "<body style=\"font:16px Segoe UI,sans-serif;max-width:620px;margin:12vh auto;padding:24px\">" +
            "<h2>MailLoadTester</h2><p>" + WebUtility.HtmlEncode(message) + "</p></body></html>");
        var reason = status == HttpStatusCode.OK ? "OK" : "Bad Request";
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {(int)status} {reason}\r\nContent-Type: text/html; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record GoogleOAuthCallback(string Code);

public sealed record GoogleOAuthAuthorizationResult(
    GoogleOAuthUser User,
    GoogleOAuthTokenSet Tokens);

public sealed class GoogleOAuthAuthorizationException : Exception
{
    public GoogleOAuthAuthorizationException(string message) : base(message) { }
}
