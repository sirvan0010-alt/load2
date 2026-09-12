namespace MailLoadTester;

/// <summary>
/// Multi-account SMTP identity. Password is in-memory only — never put into RunReport.
/// Telemetry keys use <see cref="Id"/> / <see cref="HealthKey"/>.
/// </summary>
public sealed record SmtpAccount(
    string Id,
    string SmtpHost,
    int Port,
    SmtpSecurity Security,
    bool UseAuthentication,
    string Username,
    string Password,
    SmtpAuthMethod AuthMethod = SmtpAuthMethod.Auto)
{
    public string HealthKey => $"{Id}|{SmtpHost}:{Port}";

    public static SmtpAccount FromOptions(MailTestOptions o, string? id = null) => new(
        Id: string.IsNullOrWhiteSpace(id) ? "primary" : id,
        SmtpHost: o.SmtpHost,
        Port: o.Port,
        Security: o.Security,
        UseAuthentication: o.UseAuthentication,
        Username: o.Username ?? "",
        Password: o.Password ?? "",
        AuthMethod: o.AuthMethod);

    public MailTestOptions ApplyTo(MailTestOptions baseOptions) => baseOptions with
    {
        SmtpHost = SmtpHost,
        Port = Port,
        Security = Security,
        UseAuthentication = UseAuthentication,
        Username = Username,
        Password = Password,
        AuthMethod = AuthMethod
    };
}

/// <summary>
/// Thread-safe account catalog with health-aware selection (no sockets).
/// Existing <see cref="SmtpConnectionPool"/> stays single-options; a future hub
/// can own one pool per account via <see cref="SmtpAccount.ApplyTo"/>.
/// </summary>
public sealed class SmtpAccountRegistry
{
    private readonly SmtpAccount[] _accounts;
    private int _rr;

    public SmtpAccountRegistry(IReadOnlyList<SmtpAccount> accounts)
    {
        if (accounts is null || accounts.Count == 0)
            throw new ArgumentException("At least one SMTP account is required.", nameof(accounts));
        _accounts = accounts.Select(a =>
        {
            if (string.IsNullOrWhiteSpace(a.Id))
                throw new ArgumentException("SmtpAccount.Id must be non-empty.");
            if (string.IsNullOrWhiteSpace(a.SmtpHost))
                throw new ArgumentException($"SmtpAccount '{a.Id}' has empty SmtpHost.");
            return a;
        }).ToArray();
    }

    public int Count => _accounts.Length;

    public IReadOnlyList<SmtpAccount> Snapshot() => _accounts;

    /// <summary>
    /// Prefer Healthy endpoints; fall back to Degraded; never Quarantined.
    /// Round-robin within the chosen tier. Null if every account is quarantined.
    /// </summary>
    public SmtpAccount? TrySelect(TransportHealthRegistry health, DateTimeOffset? now = null)
    {
        var t = now ?? DateTimeOffset.UtcNow;
        return Pick(health, t, EndpointHealthState.Healthy)
            ?? Pick(health, t, EndpointHealthState.Degraded);
    }

    SmtpAccount? Pick(TransportHealthRegistry health, DateTimeOffset t, EndpointHealthState required)
    {
        var start = Interlocked.Increment(ref _rr);
        if (start < 0)
        {
            Interlocked.Exchange(ref _rr, 0);
            start = 0;
        }
        var n = _accounts.Length;
        for (var k = 0; k < n; k++)
        {
            var a = _accounts[Math.Abs(start + k) % n];
            if (health.GetState(a.HealthKey, t) == required)
                return a;
        }
        return null;
    }

    public void ReportSuccess(TransportHealthRegistry health, SmtpAccount account) =>
        health.RecordSuccess(account.HealthKey);

    public void ReportFailure(TransportHealthRegistry health, SmtpAccount account, Exception? ex) =>
        health.RecordFailure(
            account.HealthKey,
            ex != null ? TransportHealthRegistry.ClassifyFailure(ex) : null);
}
