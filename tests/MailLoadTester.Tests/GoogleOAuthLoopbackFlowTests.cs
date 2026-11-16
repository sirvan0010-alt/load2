using System.Net;
using System.Text;
using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class GoogleOAuthLoopbackFlowTests
{
    [Fact]
    public void ParseCallback_ReturnsCodeWhenStateMatches()
    {
        var callback = GoogleOAuthLoopbackFlow.ParseCallback(
            "/?code=one-time-code&state=expected-state",
            "expected-state");

        Assert.Equal("one-time-code", callback.Code);
    }

    [Fact]
    public void Callback_ToStringDoesNotExposeAuthorizationCode()
    {
        var callback = new GoogleOAuthCallback("short-lived-auth-code");

        Assert.DoesNotContain("short-lived-auth-code", callback.ToString());
        Assert.Contains("[REDACTED]", callback.ToString());
    }

    [Fact]
    public void ParseCallback_RejectsMismatchedState()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GoogleOAuthLoopbackFlow.ParseCallback(
                "/?code=one-time-code&state=attacker-state",
                "expected-state"));
    }

    [Fact]
    public void ParseCallback_RejectsMissingState()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GoogleOAuthLoopbackFlow.ParseCallback(
                "/?code=one-time-code",
                "expected-state"));
    }

    [Fact]
    public void ParseCallback_RejectsMissingCode()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GoogleOAuthLoopbackFlow.ParseCallback(
                "/?state=expected-state",
                "expected-state"));
    }

    [Fact]
    public void ParseCallback_RejectsGoogleAuthorizationError()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            GoogleOAuthLoopbackFlow.ParseCallback(
                "/?error=access_denied&state=expected-state",
                "expected-state"));

        Assert.Contains("access_denied", error.Message);
    }

    [Fact]
    public void ParseCallback_RejectsUnexpectedPath()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GoogleOAuthLoopbackFlow.ParseCallback(
                "/unexpected?code=one-time-code&state=expected-state",
                "expected-state"));
    }

    [Fact]
    public async Task AuthorizeAsync_ExchangesCodeAndReturnsVerifiedGoogleAccount()
    {
        string? tokenRequestBody = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == GoogleOAuthClient.TokenEndpoint)
            {
                tokenRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return Json(HttpStatusCode.OK,
                    """{"access_token":"access-token","refresh_token":"refresh-token","expires_in":3600,"token_type":"Bearer","scope":"openid email https://www.googleapis.com/auth/gmail.send"}""");
            }

            if (request.RequestUri!.AbsoluteUri == GoogleOAuthClient.UserInfoEndpoint)
                return Json(HttpStatusCode.OK,
                    """{"sub":"google-subject","email":"user@gmail.com","email_verified":true,"name":"Google User"}""");

            throw new InvalidOperationException("Unexpected HTTP request.");
        }));
        var oauth = new GoogleOAuthClient("123456789.apps.googleusercontent.com", http);
        var flow = new GoogleOAuthLoopbackFlow();
        System.Net.Sockets.TcpClient? callbackClient = null;
        StreamReader? callbackReader = null;
        Task<string>? browserResponseTask = null;
        GoogleOAuthAuthorizationResult result;
        string browserResponse;

        try
        {
            result = await flow.AuthorizeAsync(
                oauth,
                async (authorizationUri, cancellationToken) =>
                {
                    var query = ParseQuery(authorizationUri.Query);
                    var redirect = new Uri(query["redirect_uri"]);
                    callbackClient = new System.Net.Sockets.TcpClient();
                    await callbackClient.ConnectAsync(IPAddress.Loopback, redirect.Port, cancellationToken);
                    var stream = callbackClient.GetStream();
                    var request = Encoding.ASCII.GetBytes(
                        $"GET /?code=one-time-code&state={Uri.EscapeDataString(query["state"])} HTTP/1.1\r\n" +
                        $"Host: 127.0.0.1:{redirect.Port}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(request, cancellationToken);

                    // A real Process.Start returns after launching the browser; it does
                    // not wait for the local callback response. Read concurrently so
                    // the flow can process the request and send the browser page.
                    callbackReader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    browserResponseTask = callbackReader.ReadToEndAsync(cancellationToken);
                },
                TimeSpan.FromSeconds(5));

            browserResponse = await browserResponseTask!;
        }
        finally
        {
            callbackReader?.Dispose();
            callbackClient?.Dispose();
        }

        Assert.Equal("user@gmail.com", result.User.Email);
        Assert.Equal("google-subject", result.User.Subject);
        Assert.Equal("refresh-token", result.Tokens.RefreshToken);
        Assert.Contains("code_verifier=", tokenRequestBody);
        Assert.DoesNotContain("client_secret", tokenRequestBody);
        Assert.StartsWith("HTTP/1.1 200 OK", browserResponse);
        Assert.Contains("<title>MailLoadTester</title>", browserResponse);
    }

    [Fact]
    public void ParseCallback_RejectsDuplicateSecurityParameters()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GoogleOAuthLoopbackFlow.ParseCallback(
                "/?code=first&code=second&state=expected-state",
                "expected-state"));
    }

    [Fact]
    public async Task AuthorizeAsync_TimesOutWhenBrowserNeverReturns()
    {
        using var http = new HttpClient(new NoopHandler());
        var oauth = new GoogleOAuthClient(
            "123456789.apps.googleusercontent.com", http);
        var flow = new GoogleOAuthLoopbackFlow();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            flow.AuthorizeAsync(
                oauth,
                (_, _) => Task.CompletedTask,
                TimeSpan.FromMilliseconds(50),
                CancellationToken.None));
    }

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }

    private sealed class NoopHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
    }
}
