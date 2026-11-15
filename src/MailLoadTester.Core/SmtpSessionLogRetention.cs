using System.Text;

namespace MailLoadTester;

/// <summary>
/// Bounded retention for the optional raw SMTP session log.
/// Keeps one active file (8 MiB) and at most three rotated backups.
/// </summary>
public static class SmtpSessionLogRetention
{
    public const long MaxFileBytes = 8L * 1024 * 1024;
    public const int MaxBackupFiles = 3;

    public static void StartSession(string path, bool append, string header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(header);
        PathSecurity.EnsureNoReparsePoints(path);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        if (!append || !File.Exists(path))
        {
            Rotate(path);
            File.WriteAllText(path, header, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Append(path, header);
    }

    public static void Append(string path, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(text);
        PathSecurity.EnsureNoReparsePoints(path);

        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length == 0)
            return;

        // A single flush can be larger than the file budget after a long pause.
        // Preserve its newest tail rather than allowing one oversized write.
        if (bytes.LongLength > MaxFileBytes)
            bytes = bytes[^((int)MaxFileBytes)..];

        var currentLength = File.Exists(path) ? new FileInfo(path).Length : 0;
        if (currentLength + bytes.LongLength > MaxFileBytes)
            Rotate(path);

        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void Rotate(string path)
    {
        for (var index = MaxBackupFiles; index >= 1; index--)
        {
            var destination = path + "." + index;
            PathSecurity.EnsureNoReparsePoints(destination);

            if (index == MaxBackupFiles)
            {
                if (File.Exists(destination))
                    File.Delete(destination);
                continue;
            }

            var source = path + "." + (index - 1);
            PathSecurity.EnsureNoReparsePoints(source);
            if (File.Exists(source))
            {
                if (File.Exists(destination))
                    File.Delete(destination);
                File.Move(source, destination);
            }
        }

        if (File.Exists(path))
        {
            var firstBackup = path + ".1";
            PathSecurity.EnsureNoReparsePoints(firstBackup);
            if (File.Exists(firstBackup))
                File.Delete(firstBackup);
            File.Move(path, firstBackup);
        }
    }
}
