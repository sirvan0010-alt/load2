namespace MailLoadTester.Gui;

/// <summary>Stable support identifiers shown in copyable GUI error dialogs.</summary>
internal static class GuiErrorCodes
{
    public const string Validation = "SMTP-VAL-001";
    public const string TestMessagePreparation = "SMTP-VAL-002";
    public const string SmtpSend = "SMTP-SEND-001";
    public const string SmtpConnection = "SMTP-CONN-001";
    public const string SmtpDns = "SMTP-DNS-001";
    public const string SmtpProxy = "SMTP-PROXY-001";
    public const string SmtpTls = "SMTP-TLS-001";
    public const string SmtpAuthentication = "SMTP-AUTH-001";
    public const string SmtpNetwork = "SMTP-NET-001";
    public const string SmtpProtocol = "SMTP-PROTO-001";
    public const string SmtpAccount = "SMTP-ACC-001";
    public const string TorProxy = "TOR-PROXY-001";
    public const string General = "APP-ERR-001";

    public static string ForTransportFailure(TransportFailureKind kind) => kind switch
    {
        TransportFailureKind.Dns => SmtpDns,
        TransportFailureKind.Proxy => SmtpProxy,
        TransportFailureKind.Tls => SmtpTls,
        TransportFailureKind.Authentication => SmtpAuthentication,
        TransportFailureKind.Connection or TransportFailureKind.Timeout => SmtpNetwork,
        TransportFailureKind.SmtpResponse or TransportFailureKind.Protocol => SmtpProtocol,
        _ => SmtpConnection
    };
}
