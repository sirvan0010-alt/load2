using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace MailLoadTester;

/// <summary>Read-only IMAP mailbox inspection. This service never changes mailbox state.</summary>
public sealed class MailboxMonitorService
{
    public static void ValidateOptions(MailboxMonitorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("IMAP host is required.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "IMAP port must be between 1 and 65535.");
        if (!Enum.IsDefined(options.Security))
            throw new ArgumentException("Unsupported IMAP security mode.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
            throw new ArgumentException("IMAP username and password are required.", nameof(options));
    }

    public async Task<MailboxMonitorSnapshot> ReadAsync(
        MailboxMonitorOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        cancellationToken.ThrowIfCancellationRequested();

        using var client = new ImapClient { Timeout = 15_000 };
        var socketOptions = options.Security switch
        {
            MailboxMonitorSecurity.ImplicitTls => SecureSocketOptions.SslOnConnect,
            MailboxMonitorSecurity.StartTls => SecureSocketOptions.StartTls,
            _ => throw new ArgumentOutOfRangeException(nameof(options), "Unsupported IMAP security mode.")
        };

        try
        {
            await client.ConnectAsync(options.Host.Trim(), options.Port, socketOptions, cancellationToken).ConfigureAwait(false);
            await client.AuthenticateAsync(options.Username.Trim(), options.Password, cancellationToken).ConfigureAwait(false);

            var inbox = client.Inbox;
            long? inboxSizeBytes = null;
            try
            {
                await inbox.StatusAsync(StatusItems.Count | StatusItems.Unread | StatusItems.Size, cancellationToken).ConfigureAwait(false);
                inboxSizeBytes = ToInt64(inbox.Size);
            }
            catch (Exception ex) when (ex is NotSupportedException or ImapCommandException)
            {
                // Some servers do not implement RFC 8438 STATUS SIZE. Keep count metrics.
                await inbox.StatusAsync(StatusItems.Count | StatusItems.Unread, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            uint? quotaUsedKiB = null;
            uint? quotaLimitKiB = null;
            uint? quotaMessageCount = null;
            uint? quotaMessageLimit = null;
            var status = "IMAP připojeno; server neposkytuje použitelnou STORAGE kvótu.";

            if (client.SupportsQuotas)
            {
                try
                {
                    var quota = await inbox.GetQuotaAsync(cancellationToken).ConfigureAwait(false);
                    quotaUsedKiB = quota.CurrentStorageSize;
                    quotaLimitKiB = quota.StorageLimit;
                    quotaMessageCount = quota.CurrentMessageCount;
                    quotaMessageLimit = quota.MessageLimit;
                    status = quotaUsedKiB.HasValue && quotaLimitKiB is > 0
                        ? "IMAP připojeno; STORAGE kvóta dostupná."
                        : "IMAP připojeno; server nevrátil použitelnou STORAGE kvótu.";
                }
                catch (Exception ex) when (ex is NotSupportedException or ImapCommandException)
                {
                    status = "IMAP připojeno; načtení kvóty selhalo: " + ex.Message;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return new MailboxMonitorSnapshot(
                InboxMessageCount: inbox.Count,
                InboxUnreadCount: inbox.Unread,
                InboxSizeBytes: inboxSizeBytes,
                QuotaUsedBytes: MailboxMonitorMath.ToBytes(quotaUsedKiB),
                QuotaLimitBytes: MailboxMonitorMath.ToBytes(quotaLimitKiB),
                QuotaMessageCount: quotaMessageCount,
                QuotaMessageLimit: quotaMessageLimit,
                CheckedAt: DateTimeOffset.UtcNow,
                Status: status);
        }
        finally
        {
            if (client.IsConnected)
            {
                try { await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false); }
                catch { /* Preserve the original result/error; this is best-effort cleanup. */ }
            }
        }
    }

    private static long? ToInt64(ulong? value)
        => value.HasValue && value.Value <= long.MaxValue ? (long)value.Value : null;
}