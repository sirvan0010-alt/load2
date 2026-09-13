namespace MailLoadTester;

/// <summary>
/// Draft row used by GUI / config before converting to <see cref="SmtpAccount"/>.
/// Password stays in memory only — never write into reports.
/// </summary>
public sealed record SmtpAccountDraft(
    string Id,
    string SmtpHost,
    int Port,
    SmtpSecurity Security,
    bool UseAuthentication,
    string Username,
    string Password,
    bool Enabled = true,
    SmtpAuthMethod AuthMethod = SmtpAuthMethod.Auto);

/// <summary>
/// Maps GUI/config account drafts to runner <see cref="SmtpAccount"/> list.
/// </summary>
public static class SmtpAccountMapping
{
    public static IReadOnlyList<SmtpAccount>? ToOptionsAccounts(IEnumerable<SmtpAccountDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        var enabled = new List<SmtpAccount>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in drafts)
        {
            if (d is null || !d.Enabled) continue;
            ValidateDraft(d, ids);
            ids.Add(d.Id.Trim());
            enabled.Add(new SmtpAccount(
                Id: d.Id.Trim(),
                SmtpHost: d.SmtpHost.Trim(),
                Port: d.Port,
                Security: d.Security,
                UseAuthentication: d.UseAuthentication,
                Username: d.Username ?? "",
                Password: d.Password ?? "",
                AuthMethod: d.AuthMethod));
        }

        return enabled.Count == 0 ? null : enabled;
    }

    public static void ValidateDraft(SmtpAccountDraft d, HashSet<string>? existingIds = null)
    {
        if (d is null) throw new ArgumentNullException(nameof(d));
        if (string.IsNullOrWhiteSpace(d.Id))
            throw new ArgumentException("Account Id nesmí být prázdné.");
        if (existingIds != null && existingIds.Contains(d.Id.Trim()))
            throw new ArgumentException($"Duplicitní Account Id: '{d.Id}'.");
        if (string.IsNullOrWhiteSpace(d.SmtpHost))
            throw new ArgumentException($"Account '{d.Id}': SmtpHost je povinný.");
        if (d.Port is < 1 or > 65535)
            throw new ArgumentException($"Account '{d.Id}': Port musí být 1–65535.");
        if (d.UseAuthentication && string.IsNullOrWhiteSpace(d.Username))
            throw new ArgumentException($"Account '{d.Id}': Username je povinný při autentizaci.");
        if (d.UseAuthentication && string.IsNullOrEmpty(d.Password) && d.AuthMethod != SmtpAuthMethod.OAuth2)
            throw new ArgumentException($"Account '{d.Id}': Password je povinné (kromě OAuth2).");
    }

    public static void ValidateAll(IEnumerable<SmtpAccountDraft> drafts)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in drafts)
        {
            if (d is null || !d.Enabled) continue;
            ValidateDraft(d, ids);
            ids.Add(d.Id.Trim());
        }
    }
}
