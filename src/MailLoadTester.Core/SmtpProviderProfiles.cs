namespace MailLoadTester;

/// <summary>
/// SMTP submission presets. These describe connection settings only; they never
/// contain credentials and never imply that a provider permits a particular send rate.
/// </summary>
public sealed record SmtpProviderProfile(
    string Id,
    string DisplayName,
    string Host,
    int Port,
    SmtpSecurity Security,
    string Guidance);

public static class SmtpProviderProfiles
{
    public static IReadOnlyList<SmtpProviderProfile> All { get; } =
    [
        new("Gmail", "Gmail / Google Workspace", "smtp.gmail.com", 587, SmtpSecurity.StartTls,
            "Použij celou e-mailovou adresu. Google může vyžadovat heslo aplikace nebo OAuth 2.0; aplikace sama OAuth token nezískává."),
        new("Microsoft365", "Microsoft 365", "smtp.office365.com", 587, SmtpSecurity.StartTls,
            "SMTP AUTH musí být povolené pro organizaci i schránku. Microsoft doporučuje OAuth 2.0; samotné heslo nemusí fungovat."),
        new("Outlook", "Outlook.com / Hotmail", "smtp-mail.outlook.com", 587, SmtpSecurity.StartTls,
            "Použij celou e-mailovou adresu. Způsob přihlášení závisí na zabezpečení účtu Microsoft."),
        new("Seznam", "Seznam.cz / Email.cz", "smtp.seznam.cz", 465, SmtpSecurity.ImplicitTls,
            "Seznam uvádí port 465 se SSL/TLS. Port 25 bez šifrování nepoužívej."),
        new("Yahoo", "Yahoo Mail", "smtp.mail.yahoo.com", 465, SmtpSecurity.ImplicitTls,
            "Yahoo podporuje SMTP přes SSL/TLS; obvykle je potřeba heslo aplikace."),
        new("iCloud", "Apple iCloud Mail", "smtp.mail.me.com", 587, SmtpSecurity.StartTls,
            "Použij Apple ID e-mail a heslo pro konkrétní aplikaci."),
        new("Zoho", "Zoho Mail", "smtp.zoho.com", 587, SmtpSecurity.StartTls,
            "Použij přihlašovací údaje schránky; u účtů s vícefaktorovým ověřením může být nutné heslo aplikace."),
        new("Fastmail", "Fastmail", "smtp.fastmail.com", 587, SmtpSecurity.StartTls,
            "Použij přihlašovací údaje nebo heslo aplikace podle nastavení účtu.")
    ];

    public static SmtpProviderProfile? Find(string id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static SmtpProviderProfile? FindByHost(string host) =>
        All.FirstOrDefault(p => p.Host.Equals(host?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Apply only server connection defaults. Credentials, sender, recipients,
    /// proxy, pacing and safety switches are intentionally preserved.
    /// </summary>
    public static MailTestOptions ApplyTo(MailTestOptions options, SmtpProviderProfile profile)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(profile);
        return options with
        {
            SmtpHost = profile.Host,
            Port = profile.Port,
            Security = profile.Security,
            UseAuthentication = true,
            AuthMethod = SmtpAuthMethod.Auto
        };
    }
}
