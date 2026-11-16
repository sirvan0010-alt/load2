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
