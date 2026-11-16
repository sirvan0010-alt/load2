using System.Net;
using System.Text;
using System.Text.Json;
using MailKit;
using MailLoadTester;
using MimeKit;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class GoogleGmailApiClientTests
{
    private const string ClientId = "123456789.apps.googleusercontent.com";

    [Fact]
    public async Task SendAsync_PostsBase64UrlMimeWithBearerToken()
    {
        HttpRequestMessage? captured = null;
        using var apiHttp = new HttpClient(new StubHandler(request =>
        {
            captured = CloneRequest(request);
            return Json(HttpStatusCode.OK, """{"id":"gmail-message-id","threadId":"thread-id"}""");
        }));
        using var oauthHttp = new HttpClient(new StubHandler(_ =>
            throw new InvalidOperationException("Token refresh must not be called.")));
        var oauth = new GoogleOAuthClient(ClientId, oauthHttp);
        var tokens = Tokens("access-token");
        var message = CreateMessage();
        var client = new GoogleGmailApiClient(apiHttp);

        var result = await client.SendAsync(message, oauth, tokens);

        Assert.Equal("gmail-message-id", result.MessageId);
        Assert.Equal("thread-id", result.ThreadId);
        Assert.Equal("access-token", captured!.Headers.Authorization!.Parameter);
        var payload = JsonDocument.Parse(await captured.Content!.ReadAsStringAsync());
        var raw = payload.RootElement.GetProperty("raw").GetString()!;
        Assert.DoesNotContain("=", raw);
        var mime = Encoding.UTF8.GetString(FromBase64Url(raw));
        Assert.Contains("Subject: OAuth integration test", mime);
        Assert.Contains("hello from Gmail API", mime);
    }

    [Fact]
    public async Task SendAsync_RefreshesAndRetriesExactlyOnceAfter401()
    {
        var apiCalls = 0;
        using var apiHttp = new HttpClient(new StubHandler(request =>
        {
            apiCalls++;
            if (apiCalls == 1)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            Assert.Equal("new-access-token", request.Headers.Authorization!.Parameter);
            return Json(HttpStatusCode.OK, """{"id":"sent","threadId":"thread"}""");
        }));
        using var oauthHttp = new HttpClient(new StubHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("refresh_token=refresh-token", body);
            return Json(HttpStatusCode.OK,
                """{"access_token":"new-access-token","expires_in":3600,"token_type":"Bearer","scope":"https://www.googleapis.com/auth/gmail.send"}""");
        }));
        var oauth = new GoogleOAuthClient(ClientId, oauthHttp);

        var result = await new GoogleGmailApiClient(apiHttp).SendAsync(
            CreateMessage(), oauth, Tokens("expired-access"));

        Assert.Equal(2, apiCalls);
        Assert.Equal("new-access-token", result.Tokens.AccessToken);
        Assert.Equal("refresh-token", result.Tokens.RefreshToken);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendAsync_DoesNotRetryNon401Failure(HttpStatusCode status)
    {
        var apiCalls = 0;
        using var apiHttp = new HttpClient(new StubHandler(_ =>
        {
            apiCalls++;
            return new HttpResponseMessage(status);
        }));
        using var oauthHttp = new HttpClient(new StubHandler(_ =>
            throw new InvalidOperationException("Token refresh must not be called.")));
        var oauth = new GoogleOAuthClient(ClientId, oauthHttp);

        var error = await Assert.ThrowsAsync<GoogleGmailApiException>(() =>
            new GoogleGmailApiClient(apiHttp).SendAsync(CreateMessage(), oauth, Tokens("access")));

        Assert.Equal(status, error.StatusCode);
        Assert.Equal(1, apiCalls);
    }

    private static GoogleOAuthTokenSet Tokens(string accessToken) =>
        new(accessToken, "refresh-token", DateTimeOffset.UtcNow.AddHours(1),
            "Bearer", GoogleOAuthClient.GmailSendScope);

    private static MimeMessage CreateMessage()
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@gmail.com"));
        message.To.Add(MailboxAddress.Parse("recipient@example.net"));
        message.Subject = "OAuth integration test";
        message.Body = new TextPart("plain") { Text = "hello from Gmail API" };
        return message;
    }

    private static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - base64.Length % 4) % 4);
        return Convert.FromBase64String(base64);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request) =>
        new(request.Method, request.RequestUri)
        {
            Headers = { Authorization = request.Headers.Authorization },
            Content = new StringContent(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult(),
                Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }
}
