using System.Net;
using System.Text;
using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class GoogleOAuthClientTests
{
    private const string ClientId = "717640306481-tt965q9ubsuv2u60iq1ocu9ook3hodv5.apps.googleusercontent.com";

    [Fact]
    public void PkceChallenge_UsesS256AndRandomState()
    {
        var first = GoogleOAuthClient.CreatePkceChallenge();
        var second = GoogleOAuthClient.CreatePkceChallenge();

        Assert.InRange(first.CodeVerifier.Length, 43, 128);
        Assert.Equal(43, first.CodeChallenge.Length);
        Assert.NotEqual(first.State, second.State);
        Assert.DoesNotContain("=", first.CodeVerifier);
        Assert.DoesNotContain("+", first.CodeVerifier);
        Assert.DoesNotContain("/", first.CodeVerifier);
    }

    [Fact]
    public void AuthorizationUri_RequestsOnlySendScopeAndUsesPkce()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new GoogleOAuthClient(ClientId, http);
        var pkce = GoogleOAuthClient.CreatePkceChallenge();

        var uri = client.BuildAuthorizationUri(new Uri("http://127.0.0.1:49152/"), pkce);
        var query = ParseQuery(uri.Query);

        Assert.Equal(ClientId, query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal(pkce.CodeChallenge, query["code_challenge"]);
        Assert.Equal(pkce.State, query["state"]);
        Assert.Contains(GoogleOAuthClient.GmailSendScope, query["scope"]);
        Assert.DoesNotContain("mail.google.com", query["scope"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("select_account", query["prompt"]);
    }

    [Fact]
    public void AuthorizationUri_RequestsConsentOnlyWhenExplicitlyForced()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new GoogleOAuthClient(ClientId, http);
        var uri = client.BuildAuthorizationUri(
            new Uri("http://127.0.0.1:49152/"),
            GoogleOAuthClient.CreatePkceChallenge(),
            forceConsent: true);

        Assert.Equal("consent select_account", ParseQuery(uri.Query)["prompt"]);
    }

    [Theory]
    [InlineData("http://localhost:12345/")]
    [InlineData("https://127.0.0.1:12345/")]
    [InlineData("http://192.168.1.10:12345/")]
    public void AuthorizationUri_RejectsNonLiteralHttpLoopbackRedirect(string redirect)
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new GoogleOAuthClient(ClientId, http);

        Assert.Throws<ArgumentException>(() =>
            client.BuildAuthorizationUri(new Uri(redirect), GoogleOAuthClient.CreatePkceChallenge()));
    }

    [Fact]
    public async Task ExchangeCode_DoesNotSendClientSecret_AndRequiresRefreshToken()
    {
        string? capturedBody = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json(HttpStatusCode.OK, """{"access_token":"access-value","refresh_token":"refresh-value","expires_in":3600,"token_type":"Bearer","scope":"openid email https://www.googleapis.com/auth/gmail.send"}""");
        }));
        var client = new GoogleOAuthClient(ClientId, http);

        var result = await client.ExchangeCodeAsync(
            "authorization-code",
            new Uri("http://127.0.0.1:49152/"),
            "pkce-verifier");

        Assert.Equal("access-value", result.AccessToken);
        Assert.Equal("refresh-value", result.RefreshToken);
        Assert.NotNull(capturedBody);
        Assert.Contains("code_verifier=pkce-verifier", capturedBody);
        Assert.DoesNotContain("client_secret", capturedBody);
    }

    [Fact]
    public async Task ExchangeCode_ReportsSafeOAuthErrorWithoutEchoingAuthorizationCode()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Code was already redeemed."}""")));
        var client = new GoogleOAuthClient(ClientId, http);

        var exception = await Assert.ThrowsAsync<GoogleOAuthException>(() =>
            client.ExchangeCodeAsync(
                "do-not-leak-this-code",
                new Uri("http://127.0.0.1:49152/"),
                "pkce-verifier"));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("invalid_grant", exception.OAuthError);
        Assert.Contains("invalid_grant", exception.Message);
        Assert.Contains("Code was already redeemed.", exception.Message);
        Assert.DoesNotContain("do-not-leak-this-code", exception.Message);
    }

    [Fact]
    public async Task Refresh_PreservesExistingRefreshTokenWhenGoogleOmitsIt()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            Json(HttpStatusCode.OK, """{"access_token":"new-access","expires_in":3600,"token_type":"Bearer"}""")));
        var client = new GoogleOAuthClient(ClientId, http);
        var existing = new GoogleOAuthTokenSet(
            "old-access", "long-lived-refresh", DateTimeOffset.UtcNow,
            "Bearer", GoogleOAuthClient.DefaultScopes);

        var refreshed = await client.RefreshAsync(existing);

        Assert.Equal("new-access", refreshed.AccessToken);
        Assert.Equal("long-lived-refresh", refreshed.RefreshToken);
    }

    [Fact]
    public void TokenSet_ToStringNeverExposesSecrets()
    {
        var tokens = new GoogleOAuthTokenSet(
            "access-secret-value", "refresh-secret-value", DateTimeOffset.UtcNow,
            "Bearer", GoogleOAuthClient.DefaultScopes);

        var formatted = tokens.ToString();

        Assert.DoesNotContain("access-secret-value", formatted);
        Assert.DoesNotContain("refresh-secret-value", formatted);
        Assert.Contains("[REDACTED]", formatted);
    }

    [Fact]
    public async Task GetUser_RequiresVerifiedEmail()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            Json(HttpStatusCode.OK, """{"sub":"123","email":"user@gmail.com","email_verified":true,"name":"Test User"}""")));
        var client = new GoogleOAuthClient(ClientId, http);

        var user = await client.GetUserAsync("access-token");

        Assert.Equal("user@gmail.com", user.Email);
        Assert.Equal("123", user.Subject);
    }

    [Fact]
    public async Task Revoke_SendsRefreshTokenToGoogleRevocationEndpoint()
    {
        string? capturedBody = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            Assert.Equal("https://oauth2.googleapis.com/revoke", request.RequestUri!.ToString());
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var client = new GoogleOAuthClient(ClientId, http);

        await client.RevokeAsync("refresh-token-value");

        Assert.Equal("token=refresh-token-value", capturedBody);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
