using System.Net.Sockets;
using System.Security.Authentication;
using MailKit.Net.Smtp;

namespace MailLoadTester;

public enum TransportFailureKind
{
    Dns,
    Proxy,
    Tls,
    Authentication,
    Timeout,
    Connection,
    SmtpResponse,
    Protocol,
    Cancelled,
    Unknown
}

/// <summary>
/// Classifies connection/test failures for actionable diagnostics. It inspects
/// nested exceptions because MailKit and socket layers frequently wrap the cause.
/// </summary>
public static class TransportFailureClassifier
{
    public static TransportFailureKind Classify(Exception? exception)
    {
        if (exception is null)
            return TransportFailureKind.Unknown;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                var nested = Classify(inner);
                if (nested != TransportFailureKind.Unknown)
                    return nested;
            }
        }

        if (exception is SmtpCommandException smtp)
        {
            var code = (int)smtp.StatusCode;
            var text = smtp.Message;
            if (code is 530 or 535 || ContainsAny(text, "authentication", "credentials", "login", "not authenticated"))
                return TransportFailureKind.Authentication;
            return TransportFailureKind.SmtpResponse;
        }

        if (exception.InnerException is not null)
        {
            var nested = Classify(exception.InnerException);
            if (nested != TransportFailureKind.Unknown)
                return nested;
        }

        if (exception is OperationCanceledException)
            return TransportFailureKind.Cancelled;
        if (exception is TimeoutException)
            return TransportFailureKind.Timeout;

        var typeName = exception.GetType().FullName ?? exception.GetType().Name;
        if (typeName.Contains("Proxy", StringComparison.OrdinalIgnoreCase))
            return TransportFailureKind.Proxy;

        if (exception is SocketException socket)
        {
            return socket.SocketErrorCode is SocketError.HostNotFound
                or SocketError.TryAgain
                or SocketError.NoData
                or SocketError.NoRecovery
                ? TransportFailureKind.Dns
                : TransportFailureKind.Connection;
        }

        if (exception is AuthenticationException
            || typeName.Contains("SslHandshake", StringComparison.OrdinalIgnoreCase))
            return TransportFailureKind.Tls;

        var message = exception.Message ?? string.Empty;
        if (ContainsAny(message, "proxy", "socks5", "socks4"))
            return TransportFailureKind.Proxy;
        if (ContainsAny(message, "certificate", "tls", "ssl", "handshake"))
            return TransportFailureKind.Tls;
        if (ContainsAny(message, "no such host", "host not found", "name or service not known", "dns"))
            return TransportFailureKind.Dns;
        if (ContainsAny(message, "authentication", "credentials", "login failed", "not authenticated"))
            return TransportFailureKind.Authentication;
        if (exception is SmtpProtocolException or IOException)
            return TransportFailureKind.Protocol;

        return TransportFailureKind.Unknown;
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
}
