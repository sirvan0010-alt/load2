using Xunit;
using MailKit.Net.Proxy;

namespace MailLoadTester.Tests;

public sealed class TorProxyVerifierTests
{
    [Fact]
    public void ParsesConfirmedTorExit()
    {
        var result = TorProxyVerifier.ParseCheckResponse(
            """{"IsTor":true,"IP":"185.220.101.1"}""");

        Assert.True(result.IsTor);
        Assert.Equal("185.220.101.1", result.ExitIp);
        Assert.Contains("potvrzena", result.Message);
    }

    [Fact]
    public void DoesNotAcceptNonTorExit()
    {
        var result = TorProxyVerifier.ParseCheckResponse(
            """{"IsTor":false,"IP":"203.0.113.20"}""");

        Assert.False(result.IsTor);
        Assert.Equal("203.0.113.20", result.ExitIp);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void AcceptsOnlyLoopbackSocks5Endpoint(string host)
    {
        var endpoint = TorProxyVerifier.ValidateEndpoint(new ProxyEndpoint(ProxyType.Socks5, host, 9050));
        Assert.Equal(host, endpoint.Host);
    }

    [Theory]
    [InlineData("tor.example.com")]
    [InlineData("192.0.2.10")]
    public void RejectsNonLocalProxyEndpoint(string host)
    {
        Assert.Throws<ArgumentException>(() =>
            TorProxyVerifier.ValidateEndpoint(new ProxyEndpoint(ProxyType.Socks5, host, 9050)));
    }

    [Fact]
    public void RejectsNonSocksProxy()
    {
        Assert.Throws<ArgumentException>(() =>
            TorProxyVerifier.ValidateEndpoint(new ProxyEndpoint(ProxyType.Http, "127.0.0.1", 9050)));
    }

    [Fact]
    public void SmtpHostnamesRemainHostnamesForRemoteSocksDnsResolution()
    {
        Assert.Equal(
            Socks5Client.Socks5AddressType.Domain,
            Socks5Client.GetAddressType("smtp.gmail.com", out _));
    }

    [Fact]
    public void RejectsMalformedTorCheckResponse()
    {
        Assert.Throws<InvalidDataException>(() =>
            TorProxyVerifier.ParseCheckResponse("not-json"));
    }

    [Fact]
    public void RejectsResponseWithoutValidExitIp()
    {
        Assert.Throws<InvalidDataException>(() =>
            TorProxyVerifier.ParseCheckResponse("""{"IsTor":true,"IP":"not-an-ip"}"""));
    }
}
