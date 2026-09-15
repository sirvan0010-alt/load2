namespace MailLoadTester;

public enum MailboxDeliveryDisposition
{
    Accepted,
    QuotaExceeded,
    Rejected
}

public sealed record MailboxLabMessage(
    Guid Id,
    DateTimeOffset ReceivedAt,
    string Recipient,
    string SenderDomain,
    string Workflow,
    int SizeBytes,
    SimulatedMailOutcome ProviderOutcome,
    string AuthenticationResult);

public sealed record MailboxLabQuota(
    int MaxMessages,
    long MaxBytes)
{
    public void Validate()
    {
        if (MaxMessages is < 1 or > 100_000)
            throw new ArgumentOutOfRangeException(nameof(MaxMessages));
        if (MaxBytes is < 1 or > 1_000_000_000)
            throw new ArgumentOutOfRangeException(nameof(MaxBytes));
    }
}

public sealed record MailboxDeliveryResult(
    MailboxDeliveryDisposition Disposition,
    string Mailbox,
    Guid? MessageId,
    int MessageCount,
    long UsedBytes,
    int RemainingMessages,
    long RemainingBytes,
    string Reason);

public sealed record MailboxLabSnapshot(
    string Mailbox,
    int MessageCount,
    long UsedBytes,
    int MaxMessages,
    long MaxBytes,
    double MessagePressure,
    double BytePressure,
    bool UnderPressure);

/// <summary>
/// Controlled in-memory mailbox/deliverability lab. It deliberately has no SMTP/IMAP
/// sockets and therefore cannot create external traffic. A real MTA can be attached
/// later through an adapter outside the core transport engine.
/// </summary>
public interface IMailboxDeliverabilityLab
{
    MailboxDeliveryResult Deliver(
        string mailbox,
        string senderDomain,
        string workflow,
        int sizeBytes,
        SimulatedMailOutcome providerOutcome,
        string authenticationResult,
        DateTimeOffset? receivedAt = null,
        CancellationToken cancellationToken = default);

    MailboxLabSnapshot Snapshot(string mailbox);

    IReadOnlyList<MailboxLabMessage> Read(string mailbox);

    int Recover(string mailbox, int messageCount = int.MaxValue, CancellationToken cancellationToken = default);
}

public sealed class InMemoryMailboxDeliverabilityLab : IMailboxDeliverabilityLab
{
    private sealed class MailboxState
    {
        internal readonly object Sync = new();
        internal readonly List<MailboxLabMessage> Messages = new();
        internal long UsedBytes;
        internal MailboxLabQuota Quota = new(1000, 50_000_000);
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, MailboxState> _mailboxes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly MailboxLabQuota _defaultQuota;

    public InMemoryMailboxDeliverabilityLab(MailboxLabQuota? defaultQuota = null)
    {
        _defaultQuota = defaultQuota ?? new MailboxLabQuota(1000, 50_000_000);
        _defaultQuota.Validate();
    }

    public void ConfigureMailbox(string mailbox, MailboxLabQuota quota)
    {
        ValidateMailbox(mailbox);
        ArgumentNullException.ThrowIfNull(quota);
        quota.Validate();
        var state = _mailboxes.GetOrAdd(mailbox.Trim(), _ => new MailboxState());
        lock (state.Sync)
        {
            if (state.UsedBytes > quota.MaxBytes || state.Messages.Count > quota.MaxMessages)
                throw new InvalidOperationException("New quota is below current mailbox usage.");
            state.Quota = quota;
        }
    }

    public MailboxDeliveryResult Deliver(
        string mailbox,
        string senderDomain,
        string workflow,
        int sizeBytes,
        SimulatedMailOutcome providerOutcome,
        string authenticationResult,
        DateTimeOffset? receivedAt = null,
        CancellationToken cancellationToken = default)
    {
        ValidateMailbox(mailbox);
        if (string.IsNullOrWhiteSpace(senderDomain)) throw new ArgumentException("Sender domain is required.", nameof(senderDomain));
        if (string.IsNullOrWhiteSpace(workflow)) throw new ArgumentException("Workflow is required.", nameof(workflow));
        if (sizeBytes is < 1 or > 25_000_000) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (string.IsNullOrWhiteSpace(authenticationResult)) throw new ArgumentException("Authentication result is required.", nameof(authenticationResult));
        cancellationToken.ThrowIfCancellationRequested();

        var key = mailbox.Trim();
        var state = _mailboxes.GetOrAdd(key, _ => new MailboxState { Quota = _defaultQuota });
        lock (state.Sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (providerOutcome is SimulatedMailOutcome.Throttled or SimulatedMailOutcome.TemporaryFailure or SimulatedMailOutcome.PermanentFailure)
                return BuildResult(MailboxDeliveryDisposition.Rejected, key, state, null, "provider-outcome-not-deliverable");
            if (state.Messages.Count >= state.Quota.MaxMessages || state.UsedBytes + sizeBytes > state.Quota.MaxBytes)
                return BuildResult(MailboxDeliveryDisposition.QuotaExceeded, key, state, null, "mailbox-quota-exceeded");

            var message = new MailboxLabMessage(
                Guid.NewGuid(),
                receivedAt ?? DateTimeOffset.UtcNow,
                key,
                senderDomain.Trim(),
                workflow.Trim(),
                sizeBytes,
                providerOutcome,
                authenticationResult.Trim());
            state.Messages.Add(message);
            state.UsedBytes += sizeBytes;
            return BuildResult(MailboxDeliveryDisposition.Accepted, key, state, message.Id, "accepted");
        }
    }

    public MailboxLabSnapshot Snapshot(string mailbox)
    {
        ValidateMailbox(mailbox);
        var key = mailbox.Trim();
        var state = _mailboxes.GetOrAdd(key, _ => new MailboxState { Quota = _defaultQuota });
        lock (state.Sync)
        {
            var messagePressure = (double)state.Messages.Count / state.Quota.MaxMessages;
            var bytePressure = (double)state.UsedBytes / state.Quota.MaxBytes;
            return new MailboxLabSnapshot(key, state.Messages.Count, state.UsedBytes,
                state.Quota.MaxMessages, state.Quota.MaxBytes, messagePressure, bytePressure,
                messagePressure >= 0.80 || bytePressure >= 0.80);
        }
    }

    public IReadOnlyList<MailboxLabMessage> Read(string mailbox)
    {
        ValidateMailbox(mailbox);
        var key = mailbox.Trim();
        var state = _mailboxes.GetOrAdd(key, _ => new MailboxState { Quota = _defaultQuota });
        lock (state.Sync)
            return state.Messages.ToArray();
    }

    public int Recover(string mailbox, int messageCount = int.MaxValue, CancellationToken cancellationToken = default)
    {
        ValidateMailbox(mailbox);
        if (messageCount < 1 && messageCount != int.MaxValue) throw new ArgumentOutOfRangeException(nameof(messageCount));
        var key = mailbox.Trim();
        var state = _mailboxes.GetOrAdd(key, _ => new MailboxState { Quota = _defaultQuota });
        lock (state.Sync)
        {
            var remove = Math.Min(messageCount, state.Messages.Count);
            for (var i = 0; i < remove; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.UsedBytes -= state.Messages[i].SizeBytes;
            }
            if (remove > 0) state.Messages.RemoveRange(0, remove);
            return remove;
        }
    }

    private static MailboxDeliveryResult BuildResult(MailboxDeliveryDisposition disposition, string mailbox, MailboxState state, Guid? messageId, string reason)
    {
        return new MailboxDeliveryResult(disposition, mailbox, messageId, state.Messages.Count,
            state.UsedBytes, state.Quota.MaxMessages - state.Messages.Count,
            state.Quota.MaxBytes - state.UsedBytes, reason);
    }

    private static void ValidateMailbox(string mailbox)
    {
        if (string.IsNullOrWhiteSpace(mailbox) || !mailbox.Contains('@', StringComparison.Ordinal))
            throw new ArgumentException("A valid lab mailbox is required.", nameof(mailbox));
    }
}
