using System.Diagnostics;
using MailKit.Net.Smtp;
using MimeKit;

namespace MailLoadTester;

public sealed partial class SmtpTestRunner
{
    static MimeMessage BuildMessage(
        MailTestOptions options,
        RandomTestData.GeneratedMessageContent data,
        string recipient,
        IReadOnlyList<AttachmentSource> attachments,
        IReadOnlyList<(string FileName, string Extension, byte[] Content)> inlineAttachments,
        int testId,
        bool isHtml)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(data.Name, options.From));
        message.To.Add(MailboxAddress.Parse(recipient));

        foreach (var cc in options.CcRecipients ?? Array.Empty<string>())
            message.Cc.Add(MailboxAddress.Parse(cc));
        foreach (var bcc in options.BccRecipients ?? Array.Empty<string>())
            message.Bcc.Add(MailboxAddress.Parse(bcc));

        message.Subject = TemplateTags.Process(data.Subject, testId);
        message.Headers["X-MailLoadTester-Test-ID"] = testId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var processedBody = TemplateTags.Process(data.Body, testId);
        var builder = new BodyBuilder();
        if (isHtml)
            builder.HtmlBody = processedBody;
        else
            builder.TextBody = processedBody;

        foreach (var attachment in attachments)
        {
            if (attachment.PreloadedContent is not null)
                builder.Attachments.Add(attachment.FileName, attachment.PreloadedContent);
            else
            {
                var mimePart = new MimePart("application", "octet-stream")
                {
                    Content = new MimeContent(File.OpenRead(attachment.FullPath), ContentEncoding.Default),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    FileName = attachment.FileName
                };
                builder.Attachments.Add(mimePart);
            }
        }

        if (isHtml)
        {
            foreach (var inline in inlineAttachments)
            {
                var cid = $"inline-{Guid.NewGuid():N}@mailloadtester";
                var mimePart = new MimePart("image", inline.Extension)
                {
                    Content = new MimeContent(new MemoryStream(inline.Content), ContentEncoding.Base64),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                    ContentId = cid,
                    FileName = inline.FileName
                };
                builder.LinkedResources.Add(mimePart);
                var nameNoExt = Path.GetFileNameWithoutExtension(inline.FileName);
                builder.HtmlBody = builder.HtmlBody?.Replace($"{{{{cid:{nameNoExt}}}}}", $"cid:{cid}")
                    ?? $"<img src=\"cid:{cid}\" />";
            }
        }

        message.Body = builder.ToMessageBody();

        foreach (var kv in options.CustomHeaders)
        {
            if (kv.Key.Equals("From", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("To", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Subject", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Message-Id", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("MIME-Version", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                continue;
            message.Headers[kv.Key] = kv.Value;
        }

        return message;
    }

    static bool IsTransient(Exception ex)
    {
        if (ex is SmtpCommandException sce)
            return (int)sce.StatusCode is >= 400 and < 500;
        if (ex is SmtpProtocolException) return true;
        if (ex is IOException or TimeoutException) return true;
        return false;
    }

    static TimeSpan GetRetryDelay(int attempt)
    {
        var baseMs = Math.Min(8_000, 500 * Math.Pow(2, attempt));
        var jitter = Random.Shared.NextDouble() * 0.4 - 0.2;
        return TimeSpan.FromMilliseconds(Math.Max(100, baseMs * (1 + jitter)));
    }

    static double? EstimateEta(int done, int total, DateTime start, Stopwatch sw)
    {
        if (done <= 0) return null;
        var elapsed = sw.Elapsed.TotalSeconds;
        if (elapsed < 0.5) return null;
        var rate = done / elapsed;
        if (rate <= 0) return null;
        return (total - done) / rate;
    }

    static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        if (sorted.Length == 1) return sorted[0];
        var idx = p * (sorted.Length - 1);
        var lo = (int)Math.Floor(idx);
        var hi = (int)Math.Ceiling(idx);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
    }
}

sealed class CountingStream : Stream
{
    public long BytesWritten { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => BytesWritten;
    public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => BytesWritten += count;
    public override void Write(ReadOnlySpan<byte> buffer) => BytesWritten += buffer.Length;
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        BytesWritten += count;
        return Task.CompletedTask;
    }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        BytesWritten += buffer.Length;
        return default;
    }
}
