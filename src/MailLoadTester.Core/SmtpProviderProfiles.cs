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
    SmtpAuthMethod AuthMethod,
    string Guidance);

public static class SmtpProviderProfiles
{
    public static IReadOnlyList<SmtpProviderProfile> All { get; } =
    [
        new("Gmail", "Gmail / Google Workspace", "smtp.gmail.com", 587, SmtpSecurity.StartTls, SmtpAuthMethod.Auto,
            "Použij celou e-mailovou adresu a 16znakové heslo aplikace (vyžaduje dvoufázové ověření). OAuth2 access token lze zadat ručně, ale aplikace jej zatím sama nezískává."),
        new("Microsoft365", "Microsoft 365", "smtp.office365.com", 587, SmtpSecurity.StartTls, SmtpAuthMethod.OAuth2,
            "SMTP AUTH musí být povolené pro organizaci i schránku. Microsoft doporučuje OAuth 2.0. Do pole Heslo vlož platný access token; aplikace jej sama nezískává."),
        new("Outlook", "Outlook.com / Hotmail", "smtp-mail.outlook.com", 587, SmtpSecurity.StartTls, SmtpAuthMethod.OAuth2,
            "Outlook.com vyžaduje Modern Auth / OAuth2. Do pole Heslo vlož platný access token; aplikace jej sama nezískává."),
        new("Seznam", "Seznam.cz / Email.cz", "smtp.seznam.cz", 465, SmtpSecurity.ImplicitTls, SmtpAuthMethod.Auto,
            "Seznam uvádí port 465 se SSL/TLS a vyžaduje SMTP autentizaci. Port 25 bez šifrování nepoužívej."),
        new("Yahoo", "Yahoo Mail", "smtp.mail.yahoo.com", 465, SmtpSecurity.ImplicitTls, SmtpAuthMethod.Auto,
            "Yahoo podporuje SMTP přes SSL/TLS a vyžaduje autentizaci; obvykle je potřeba heslo aplikace."),
        new("iCloud", "Apple iCloud Mail", "smtp.mail.me.com", 587, SmtpSecurity.StartTls, SmtpAuthMethod.Auto,
            "Použij celou iCloud e-mailovou adresu a heslo pro konkrétní aplikaci."),
        new("Zoho", "Zoho Mail (osobní / standardní)", "smtp.zoho.com", 587, SmtpSecurity.StartTls, SmtpAuthMethod.Auto,
            "U placených doménových účtů se může lišit server (např. smtppro.zoho.com) i datacentrum. Ověř údaje ve svém Zoho účtu; port 465/SSL je také podporován."),
        new("ZohoEU", "Zoho Mail (EU)", "smtp.zoho.eu", 587, SmtpSecurity.StartTls, SmtpAuthMethod.Auto,
            "Regionální server pro účty v EU. U placených doménových účtů ověř přesný server a datacentrum v nastavení Zoho."),
        new("Fastmail", "Fastmail", "smtp.fastmail.com", 465, SmtpSecurity.ImplicitTls, SmtpAuthMethod.Plain,
            "Fastmail vyžaduje heslo aplikace a uvádí autentizaci PLAIN. Alternativně lze použít port 587 se STARTTLS.")
    ];

    public static SmtpProviderProfile? Find(string id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static SmtpProviderProfile? FindByHost(string host)
    {
        var normalized = host?.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return All.FirstOrDefault(p =>
            p.Host.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Best-effort detection from a mailbox domain; custom-domain Workspace accounts need an SMTP host.</summary>
    public static SmtpProviderProfile? FindByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1)
            return null;

        var domain = email[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();
        if (domain is "gmail.com" or "googlemail.com")
            return Find("Gmail");
        if (domain is "outlook.com" or "hotmail.com" or "live.com" or "msn.com")
            return Find("Outlook");
        if (domain is "seznam.cz" or "email.cz" or "post.cz" or "spoluzaci.cz")
            return Find("Seznam");
        if (domain.StartsWith("yahoo.", StringComparison.Ordinal) || domain == "ymail.com" || domain == "rocketmail.com")
            return Find("Yahoo");
        if (domain is "icloud.com" or "me.com" or "mac.com")
            return Find("iCloud");
        if (domain is "zoho.com" or "zohomail.com" or "zoho.eu")
            return domain == "zoho.eu" ? Find("ZohoEU") : Find("Zoho");
        if (domain is "fastmail.com" or "fastmail.fm" or "messagingengine.com")
            return Find("Fastmail");

        return null;
    }

    /// <summary>Prefer an explicitly configured SMTP host; fall back to the mailbox domain.</summary>
    public static SmtpProviderProfile? Detect(string host, string email) =>
        FindByHost(host) ?? FindByEmail(email);

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
            AuthMethod = profile.AuthMethod
        };
    }
}
