namespace MailLoadTester;

/// <summary>
/// Direct-MX domain selection and safety checks (BUG-008).
/// The connection pool is keyed to a single SmtpHost for a run. Therefore Direct MX
/// must resolve exactly one recipient domain. Mixed-domain configurations are rejected
/// here as well as in <see cref="Validation"/> so bypassing validation cannot silently
/// route every message to the first recipient's MX.
/// </summary>
public static class DirectMxRouting
{
    /// <summary>
    /// Extracts the unique recipient domain for Direct MX delivery.
    /// Throws if there are no valid domains or more than one distinct domain.
    /// </summary>
    public static string RequireSingleRecipientDomain(IReadOnlyList<string> recipients)
    {
        if (recipients is null || recipients.Count == 0)
            throw new ArgumentException("Direct MX delivery vyžaduje alespoň jednoho příjemce.");

        string? single = null;
        foreach (var r in recipients)
        {
            var domain = ExtractDomain(r);
            if (string.IsNullOrEmpty(domain))
                throw new ArgumentException($"Direct MX: neplatný příjemce bez domény: '{r}'.");

            if (single is null)
            {
                single = domain;
                continue;
            }

            if (!string.Equals(single, domain, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Direct MX delivery vyžaduje příjemce z jedné domény; " +
                    "při více doménách by se všem zprávám použil MX server první domény. " +
                    $"Nalezeno: '{single}' a '{domain}'.");
            }
        }

        if (single is null)
            throw new ArgumentException("Direct MX delivery: žádná platná doména příjemce.");

        return single;
    }

    /// <summary>Returns the domain part of an address, normalized (lower, no trailing dot), or null.</summary>
    public static string? ExtractDomain(string? recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient))
            return null;
        var at = recipient.LastIndexOf('@');
        if (at < 0 || at >= recipient.Length - 1)
            return null;
        var domain = recipient[(at + 1)..].Trim().TrimEnd('.').ToLowerInvariant();
        return string.IsNullOrEmpty(domain) ? null : domain;
    }
}
