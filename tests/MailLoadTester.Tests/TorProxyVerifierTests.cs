using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task ProxyConnectReceivesOriginalHostnameForRemoteDnsResolution()
    {
        var proxy = new RecordingProxyClient();
        using var stream = await TorProxyVerifier.ConnectThroughProxyAsync(
            proxy, new DnsEndPoint("smtp.gmail.com", 587), CancellationToken.None);

        Assert.Equal("smtp.gmail.com", proxy.LastHost);
        Assert.Equal(587, proxy.LastPort);
    }

    private sealed class RecordingProxyClient : IProxyClient
    {
        public NetworkCredential? ProxyCredentials => null;
        public string ProxyHost => "127.0.0.1";
        public int ProxyPort => 9050;
        public IPEndPoint? LocalEndPoint { get; set; }
        public string? LastHost { get; private set; }
        public int LastPort { get; private set; }

        public Stream Connect(string host, int port, CancellationToken cancellationToken = default)
        {
            LastHost = host;
            LastPort = port;
            return new MemoryStream();
        }

        public Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            LastHost = host;
            LastPort = port;
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public Stream Connect(string host, int port, int timeout, CancellationToken cancellationToken = default)
            => Connect(host, port, cancellationToken);

        public Task<Stream> ConnectAsync(string host, int port, int timeout, CancellationToken cancellationToken = default)
            => ConnectAsync(host, port, cancellationToken);
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
