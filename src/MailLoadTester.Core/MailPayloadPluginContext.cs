namespace MailLoadTester;

/// <summary>
/// Stable, network-independent context supplied to an <see cref="IMailPayloadPlugin"/>.
/// It deliberately exposes no SMTP client, connection pool, proxy, or transport API.
/// </summary>
public sealed record MailPayloadPluginContext(
    int TestId,
    string Recipient,
    int AttemptIndex,
    CancellationToken CancellationToken);
