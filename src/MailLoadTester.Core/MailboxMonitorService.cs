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

            IReadOnlyList<MailboxFolderSnapshot> folderSnapshots;
            try
            {
                folderSnapshots = await ReadFolderInventoryAsync(client, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is NotSupportedException or ImapCommandException)
            {
                // Folder listing is an optional enhancement; do not lose quota/INBOX metrics
                // merely because a provider restricts LIST or special-use discovery.
                folderSnapshots = Array.Empty<MailboxFolderSnapshot>();
            }

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
                Status: status,
                Folders: folderSnapshots);
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


    private static async Task<IReadOnlyList<MailboxFolderSnapshot>> ReadFolderInventoryAsync(
        ImapClient client,
        CancellationToken cancellationToken)
    {
        var result = new List<MailboxFolderSnapshot>();
        if (client.PersonalNamespaces.Count == 0)
            return result;

        var pending = new Queue<IMailFolder>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = await client.GetFoldersAsync(client.PersonalNamespaces[0], cancellationToken).ConfigureAwait(false);
        foreach (var folder in roots)
            pending.Enqueue(folder);

        const int maxFolders = 100;
        while (pending.Count > 0 && result.Count < maxFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Dequeue();
            if (!visited.Add(folder.FullName))
                continue;

            int? count = null;
            int? unread = null;
            long? size = null;
            try
            {
                await folder.StatusAsync(
                    StatusItems.Count | StatusItems.Unread | StatusItems.Size,
                    cancellationToken).ConfigureAwait(false);
                count = folder.Count;
                unread = folder.Unread;
                size = ToInt64(folder.Size);
            }
            catch (Exception ex) when (ex is NotSupportedException or ImapCommandException)
            {
                try
                {
                    await folder.StatusAsync(StatusItems.Count | StatusItems.Unread, cancellationToken)
                        .ConfigureAwait(false);
                    count = folder.Count;
                    unread = folder.Unread;
                }
                catch (Exception nested) when (nested is NotSupportedException or ImapCommandException)
                {
                    // A folder that cannot return STATUS is still listed with unknown metrics.
                }
            }

            var attributes = folder.Attributes;
            var isInbox = attributes.HasFlag(FolderAttributes.Inbox) ||
                string.Equals(folder.Name, "INBOX", StringComparison.OrdinalIgnoreCase);
            var isJunk = attributes.HasFlag(FolderAttributes.Junk) || IsLikelyJunkFolder(folder.Name);
            result.Add(new MailboxFolderSnapshot(
                Name: folder.Name,
                FullName: folder.FullName,
                IsInbox: isInbox,
                IsJunk: isJunk,
                IsSent: attributes.HasFlag(FolderAttributes.Sent),
                IsTrash: attributes.HasFlag(FolderAttributes.Trash),
                MessageCount: count,
                UnreadCount: unread,
                SizeBytes: size));

            if (result.Count >= maxFolders)
                break;

            try
            {
                var children = await folder.GetSubfoldersAsync(false, StatusItems.None, cancellationToken).ConfigureAwait(false);
                foreach (var child in children)
                    pending.Enqueue(child);
            }
            catch (Exception ex) when (ex is NotSupportedException or ImapCommandException)
            {
                // Some servers do not expose recursive folder listing.
            }
        }

        return result;
    }

    private static bool IsLikelyJunkFolder(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return normalized is "junk" or "spam" or "bulk mail" or "junk e-mail" or
            "nevyžádaná pošta" or "spamové zprávy" or "nevyžádané" or
            "courrier indésirable" or "correo no deseado";
    }

    private static long? ToInt64(ulong? value)
        => value.HasValue && value.Value <= long.MaxValue ? (long)value.Value : null;
}