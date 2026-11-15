namespace MailLoadTester.Gui;

/// <summary>Stable support identifiers shown in copyable GUI error dialogs.</summary>
internal static class GuiErrorCodes
{
    public const string Validation = "SMTP-VAL-001";
    public const string TestMessagePreparation = "SMTP-VAL-002";
    public const string SmtpSend = "SMTP-SEND-001";
    public const string SmtpConnection = "SMTP-CONN-001";
    public const string SmtpAccount = "SMTP-ACC-001";
    public const string TorProxy = "TOR-PROXY-001";
    public const string General = "APP-ERR-001";
}
