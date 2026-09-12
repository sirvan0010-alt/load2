namespace MailLoadTester;

public enum SmtpAuthMethod { Auto, Plain, Login, CramMd5, ScramSha1, Ntlm, OAuth2 }
public enum IpVersionPreference { Any, IPv4Only, IPv6Only, DualStack }
public enum SmtpSecurity { None, StartTls, SslOnConnect }

// NOTE: This is a temporary partial restore - full content follows from artifact.
public static class Validation
{
    public static bool IsValidEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { var _ = new System.Net.Mail.MailAddress(value); return value.Contains('@'); }
        catch { return false; }
    }

    public static bool ContainsPathTraversal(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var n = path.Replace('\\', '/');
        return n.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(s => s == "..");
    }

    public static string ExplainSmtpError(Exception ex)
    {
        if (ex is null) return "";
        if (ex is MailPayloadPluginException mpe) return mpe.Message;
        if (ex is MailKit.Net.Smtp.SmtpCommandException sce)
            return $"{(int)sce.StatusCode} {sce.Message}".Trim();
        if (ex is MailKit.Net.Smtp.SmtpProtocolException)
            return "SMTP protokol: " + ex.Message;
        if (ex is TimeoutException) return "Timeout: " + ex.Message;
        if (ex is OperationCanceledException) return "Zrušeno uživatelem / tokenem";
        if (ex is IOException) return "IO: " + ex.Message;
        if (ex is MailKit.Security.AuthenticationException) return "AUTH: " + ex.Message;
        return ex.GetType().Name + ": " + ex.Message;
    }

    public static void Validate(MailTestOptions o)
    {
        if (o is null) throw new ArgumentNullException(nameof(o));
        if (!IsValidEmail(o.From)) throw new ArgumentException("From musí být platný e-mail.");
        if (o.Recipients is null || o.Recipients.Count == 0)
            throw new ArgumentException("Alespoň jeden příjemce je povinný.");
        foreach (var r in o.Recipients)
            if (!IsValidEmail(r)) throw new ArgumentException($"Neplatný příjemce: {r}");

        if (!string.IsNullOrEmpty(o.EmlTemplatePath) && ContainsPathTraversal(o.EmlTemplatePath))
            throw new ArgumentException("EML cesta nesmí obsahovat '..'.");
        if (!string.IsNullOrEmpty(o.SessionLogPath) && ContainsPathTraversal(o.SessionLogPath))
            throw new ArgumentException("Session log cesta nesmí obsahovat '..'.");

        if (o.Attachments != null)
            foreach (var path in o.Attachments)
                if (ContainsPathTraversal(path))
                    throw new ArgumentException("Cesta přílohy nesmí obsahovat '..'.");

        if (o.InlineAttachments != null)
            foreach (var path in o.InlineAttachments)
                if (ContainsPathTraversal(path))
                    throw new ArgumentException("Cesta inline přílohy nesmí obsahovat '..'.");

        if (!string.IsNullOrEmpty(o.ClientCertificatePath) && ContainsPathTraversal(o.ClientCertificatePath))
            throw new ArgumentException("Cesta klientského certifikátu nesmí obsahovat '..'.");

        if (string.IsNullOrWhiteSpace(o.SmtpHost) && !o.DirectMxDelivery)
            throw new ArgumentException("SMTP host je povinný (nebo Direct MX).");
        if (o.MessageCount < 1) throw new ArgumentException("MessageCount musí být >= 1.");
        if (o.MaxConcurrency < 1) throw new ArgumentException("MaxConcurrency musí být >= 1.");
        if (o.Port is < 1 or > 65535) throw new ArgumentException("Port mimo rozsah.");
    }

    public static string[] ParseDomains(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? Array.Empty<string>() :
        raw.Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    public static IReadOnlyDictionary<string, string> ParseHeaders(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return dict;
        foreach (var line in raw.Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t) || t.StartsWith('#')) continue;
            var i = t.IndexOf(':');
            if (i <= 0) continue;
            dict[t[..i].Trim()] = t[(i + 1)..].Trim();
        }
        return dict;
    }
}
