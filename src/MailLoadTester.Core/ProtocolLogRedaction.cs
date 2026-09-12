using System.Text;
using MailKit;

namespace MailLoadTester;

/// <summary>
/// Shared redaction for MailKit <see cref="IProtocolLogger"/> implementations (SEC-AUDIT-001).
/// MailKit sets <see cref="IAuthenticationSecretDetector"/> on the logger; loggers must
/// apply detected secret ranges before writing bytes to session logs or path observers.
/// </summary>
public static class ProtocolLogRedaction
{
    public static string DecodeClientOrServer(
        byte[] buffer,
        int offset,
        int count,
        IAuthenticationSecretDetector? detector)
    {
        if (count <= 0 || buffer is null)
            return string.Empty;

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        if (detector is null)
            return Encoding.ASCII.GetString(buffer, offset, count).TrimEnd('\r', '\n');

        var secrets = detector.DetectSecrets(buffer, offset, count);
        if (secrets is null || secrets.Count == 0)
            return Encoding.ASCII.GetString(buffer, offset, count).TrimEnd('\r', '\n');

        var copy = new byte[count];
        Buffer.BlockCopy(buffer, offset, copy, 0, count);
        foreach (var secret in secrets)
        {
            // MailKit reports StartIndex as an absolute index into `buffer`.
            var start = secret.StartIndex - offset;
            var end = start + secret.Length;
            if (start < 0) start = 0;
            if (end > count) end = count;
            for (var i = start; i < end; i++)
                copy[i] = (byte)'*';
        }

        return Encoding.ASCII.GetString(copy).TrimEnd('\r', '\n');
    }
}
