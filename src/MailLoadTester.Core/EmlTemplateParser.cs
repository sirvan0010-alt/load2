using MimeKit;

namespace MailLoadTester;

public static class EmlTemplateParser
{
    public const long DefaultMaxFileBytes = 64L * 1024 * 1024;
    public const long DefaultMaxAttachmentBytes = 256L * 1024 * 1024;
    public const int MaxBodyCharacters = 10_000_000;

    public static EmlTemplate Parse(
        string path,
        long maxFileBytes = DefaultMaxFileBytes,
        long maxAttachmentBytes = DefaultMaxAttachmentBytes)
    {
        PathSecurity.EnsureNoReparsePoints(path);
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException($"EML šablona neexistuje: {path}", path);
        if (info.Length > maxFileBytes)
            throw new InvalidOperationException(
                $"EML šablona je příliš velká ({AttachmentPlanner.FormatBytes(info.Length)}). " +
                $"Bezpečnostní limit je {AttachmentPlanner.FormatBytes(maxFileBytes)}.");
        if (maxAttachmentBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttachmentBytes));

        using var message = MimeMessage.Load(path);
        var subject = message.Subject ?? "MailLoadTester test";
        var htmlBody = message.HtmlBody;
        var isHtml = htmlBody != null;
        var body = htmlBody ?? message.TextBody ?? "";
        if (body.Length > MaxBodyCharacters)
            throw new InvalidOperationException(
                $"Tělo EML šablony je příliš velké ({body.Length:N0} znaků). Limit je {MaxBodyCharacters:N0} znaků.");

        var attachments = new List<(string FileName, byte[] Content)>();
        long totalDecoded = 0;
        foreach (var part in message.Attachments.OfType<MimePart>())
        {
            var remaining = maxAttachmentBytes - totalDecoded;
            if (remaining <= 0)
                throw new InvalidOperationException(
                    $"Celková velikost dekódovaných EML příloh překročila bezpečný limit " +
                    $"{AttachmentPlanner.FormatBytes(maxAttachmentBytes)}.");

            using var ms = new MemoryStream(EstimateInitialCapacity(part, remaining));
            using var quota = new QuotaWriteStream(ms, remaining);
            try
            {
                part.Content.DecodeTo(quota);
            }
            catch (QuotaExceededException)
            {
                throw new InvalidOperationException(
                    $"EML příloha '{part.FileName ?? "attachment"}' by překročila bezpečný limit " +
                    $"{AttachmentPlanner.FormatBytes(maxAttachmentBytes)} pro dekódované přílohy.");
            }

            var content = ms.ToArray();
            totalDecoded = checked(totalDecoded + content.LongLength);
            attachments.Add((part.FileName ?? "attachment", content));
        }

        return new EmlTemplate(subject, body, isHtml, attachments);
    }

    private sealed class QuotaExceededException : IOException { }

    private static int EstimateInitialCapacity(MimePart part, long remaining)
    {
        const int fallback = 64 * 1024;
        long estimate = fallback;
        try
        {
            if (part.Content?.Stream is { CanSeek: true } stream)
            {
                estimate = part.Content.Encoding switch
                {
                    ContentEncoding.Base64 or ContentEncoding.UUEncode => (long)(stream.Length * 0.75),
                    _ => stream.Length
                };
            }
        }
        catch
        {
            estimate = fallback;
        }

        estimate = Math.Max(fallback, estimate);
        estimate = Math.Min(estimate, remaining);
        return (int)Math.Min(estimate, int.MaxValue - 1024);
    }

    private sealed class QuotaWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _limit;
        private long _written;

        public QuotaWriteStream(Stream inner, long limit)
        {
            _inner = inner;
            _limit = limit;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public override long Position { get => _written; set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureQuota(count);
            _inner.Write(buffer, offset, count);
            _written += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureQuota(buffer.Length);
            _inner.Write(buffer);
            _written += buffer.Length;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureQuota(count);
            await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
            _written += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureQuota(buffer.Length);
            await _inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            _written += buffer.Length;
        }

        private void EnsureQuota(int count)
        {
            if (count < 0 || _written > _limit - count)
                throw new QuotaExceededException();
        }
    }
}

public sealed record EmlTemplate(
    string Subject,
    string Body,
    bool IsHtml,
    IReadOnlyList<(string FileName, byte[] Content)> Attachments);
