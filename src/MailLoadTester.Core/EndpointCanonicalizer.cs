namespace MailLoadTester;

/// <summary>
/// Canonicalizes transport identifiers used for comparison, health and deduplication.
/// It does not resolve DNS and deliberately does not alter SMTP local-part semantics.
/// </summary>
public static class EndpointCanonicalizer
{
    public static string Host(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Endpoint host must be non-empty.", nameof(value));

        var host = value.Trim().TrimEnd('.');
        if (host.Length == 0)
            throw new ArgumentException("Endpoint host must be non-empty.", nameof(value));

        // DNS names are case-insensitive. IP literals are normalized by IPAddress
        // where possible; non-DNS host tokens are left intact apart from trimming.
        if (System.Net.IPAddress.TryParse(host, out var ip))
            return ip.ToString();

        return host.ToLowerInvariant();
    }

    public static string Smtp(string host, int port, SmtpSecurity security) =>
        $"{Host(host)}:{port}/{security}";

    public static string Email(string value)
    {
        if (!Validation.IsValidEmail(value))
            throw new ArgumentException("Invalid e-mail address.", nameof(value));

        var trimmed = value.Trim();
        var at = trimmed.LastIndexOf('@');
        if (at <= 0 || at == trimmed.Length - 1)
            throw new ArgumentException("Invalid e-mail address.", nameof(value));

        // Preserve the local part exactly; normalize only the DNS domain.
        return trimmed[..(at + 1)] + Host(trimmed[(at + 1)..]);
    }
}
