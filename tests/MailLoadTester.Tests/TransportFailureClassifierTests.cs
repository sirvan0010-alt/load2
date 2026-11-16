using System.Net.Sockets;
using System.Security.Authentication;
using MailKit;
using MailKit.Net.Smtp;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class TransportFailureClassifierTests
{
    [Fact]
    public void ClassifiesDnsSocketErrors()
    {
        var ex = new SocketException((int)SocketError.HostNotFound);
        Assert.Equal(TransportFailureKind.Dns, TransportFailureClassifier.Classify(ex));
    }

    [Fact]
    public void ClassifiesConnectionSocketErrors()
    {
        var ex = new SocketException((int)SocketError.ConnectionRefused);
        Assert.Equal(TransportFailureKind.Connection, TransportFailureClassifier.Classify(ex));
    }

    [Fact]
    public void ClassifiesTlsAuthenticationException()
    {
        Assert.Equal(TransportFailureKind.Tls,
            TransportFailureClassifier.Classify(new AuthenticationException("TLS handshake failed")));
    }

    [Fact]
    public void ClassifiesProxyFailureFromWrappedMessage()
    {
        var ex = new IOException("Connection failed", new InvalidOperationException("SOCKS5 proxy handshake failed"));
        Assert.Equal(TransportFailureKind.Proxy, TransportFailureClassifier.Classify(ex));
    }

    [Theory]
    [InlineData(530, "5.7.0 authentication required")]
    [InlineData(535, "5.7.8 authentication credentials invalid")]
    public void ClassifiesSmtpAuthenticationRejections(int code, string message)
    {
        var ex = new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, (SmtpStatusCode)code, message);
        Assert.Equal(TransportFailureKind.Authentication, TransportFailureClassifier.Classify(ex));
    }

    [Fact]
    public void ClassifiesOtherSmtpRejectionsSeparately()
    {
        var ex = new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, (SmtpStatusCode)550, "mailbox unavailable");
        Assert.Equal(TransportFailureKind.SmtpResponse, TransportFailureClassifier.Classify(ex));
    }

    [Fact]
    public void ClassifiesTimeoutAndCancellation()
    {
        Assert.Equal(TransportFailureKind.Timeout, TransportFailureClassifier.Classify(new TimeoutException()));
        Assert.Equal(TransportFailureKind.Cancelled, TransportFailureClassifier.Classify(new OperationCanceledException()));
    }

    [Fact]
    public void PrefersNestedSocketCause()
    {
        var ex = new IOException("Network error", new SocketException((int)SocketError.HostNotFound));
        Assert.Equal(TransportFailureKind.Dns, TransportFailureClassifier.Classify(ex));
    }

    [Fact]
    public void UnknownForUnclassifiedException()
    {
        Assert.Equal(TransportFailureKind.Unknown,
            TransportFailureClassifier.Classify(new InvalidOperationException("unexpected state")));
    }
}
