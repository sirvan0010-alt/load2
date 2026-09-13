namespace MailLoadTester;

/// <summary>
/// Validated recipient set for a scenario. Does not own SMTP transport, credentials, or pacing.
/// </summary>
public sealed class TargetSet
{
    public const int MaxTargets = 50_000;

    public IReadOnlyList<string> Recipients { get; }
    public string? SourcePath { get; }

    TargetSet(IReadOnlyList<string> recipients, string? sourcePath)
    {
        Recipients = recipients;
        SourcePath = sourcePath;
    }

    public int Count => Recipients.Count;

    public static TargetSet FromRecipients(IEnumerable<string> recipients, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in recipients)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var email = raw.Trim();
            if (!seen.Add(email)) continue;
            if (!Validation.IsValidEmail(email))
                throw new ArgumentException($"Neplatný e-mail v TargetSet: '{email}'.");
            list.Add(email);
            if (list.Count > MaxTargets)
                throw new ArgumentException($"TargetSet přesahuje limit {MaxTargets} adres.");
        }
        if (list.Count == 0)
            throw new ArgumentException("TargetSet musí obsahovat alespoň jednoho příjemce.");
        return new TargetSet(list, sourcePath);
    }

    public static async Task<TargetSet> FromFileAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Cesta k target souboru je prázdná.");
        PathSecurity.EnsureNoReparsePoints(path);
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new FileNotFoundException("Target soubor neexistuje.", full);

        var lines = new List<string>();
        await foreach (var line in File.ReadLinesAsync(full, ct).ConfigureAwait(false))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#')) continue;
            var email = t.Split(',', ';')[0].Trim();
            if (email.Length > 0)
                lines.Add(email);
        }
        return FromRecipients(lines, full);
    }

    public MailTestOptions ApplyTo(MailTestOptions options) =>
        options with { Recipients = Recipients };
}

public sealed record ScenarioLimits(
    int MessageCount = 1,
    int DurationSeconds = 0)
{
    public const int MaxDurationSeconds = 86_400;

    public void Validate()
    {
        if (MessageCount is < 1 or > 10_000)
            throw new ArgumentException("Scenario MessageCount musí být 1–10000.");
        if (DurationSeconds is < 0 or > MaxDurationSeconds)
            throw new ArgumentException($"DurationSeconds musí být 0–{MaxDurationSeconds}.");
    }

    public MailTestOptions ApplyTo(MailTestOptions options) =>
        options with
        {
            MessageCount = MessageCount,
            DurationSeconds = DurationSeconds
        };
}

public sealed record SmtpScenario(TargetSet Targets, ScenarioLimits Limits)
{
    public MailTestOptions ApplyTo(MailTestOptions baseOptions)
    {
        Limits.Validate();
        return Limits.ApplyTo(Targets.ApplyTo(baseOptions));
    }
}
