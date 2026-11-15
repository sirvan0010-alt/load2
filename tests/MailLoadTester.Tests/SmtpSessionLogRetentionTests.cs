using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmtpSessionLogRetentionTests
{
    [Fact]
    public void StartSession_RotatesPreviousFileAndRetainsOnlyThreeBackups()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mlt-log-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "smtp-session.log");
        try
        {
            File.WriteAllText(path, "active-old");
            File.WriteAllText(path + ".1", "backup-1");
            File.WriteAllText(path + ".2", "backup-2");
            File.WriteAllText(path + ".3", "backup-3");

            SmtpSessionLogRetention.StartSession(path, append: false, "new-session");

            Assert.Equal("new-session", File.ReadAllText(path));
            Assert.Equal("active-old", File.ReadAllText(path + ".1"));
            Assert.Equal("backup-1", File.ReadAllText(path + ".2"));
            Assert.Equal("backup-2", File.ReadAllText(path + ".3"));
            Assert.False(File.Exists(path + ".4"));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Append_RotatesBeforeTheActiveFileWouldExceedEightMiB()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mlt-log-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "smtp-session.log");
        try
        {
            File.WriteAllBytes(path, new byte[(int)SmtpSessionLogRetention.MaxFileBytes - 10]);
            SmtpSessionLogRetention.Append(path, new string('x', 20));

            Assert.Equal(20, new FileInfo(path).Length);
            Assert.Equal(SmtpSessionLogRetention.MaxFileBytes - 10, new FileInfo(path + ".1").Length);
            Assert.True(new FileInfo(path).Length <= SmtpSessionLogRetention.MaxFileBytes);
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RepeatedRotations_NeverKeepMoreThanThreeBackups()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mlt-log-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "smtp-session.log");
        try
        {
            for (var i = 0; i < 6; i++)
            {
                SmtpSessionLogRetention.StartSession(path, append: false, "session-" + i);
            }

            Assert.Equal("session-5", File.ReadAllText(path));
            Assert.Equal("session-4", File.ReadAllText(path + ".1"));
            Assert.Equal("session-3", File.ReadAllText(path + ".2"));
            Assert.Equal("session-2", File.ReadAllText(path + ".3"));
            Assert.False(File.Exists(path + ".4"));
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }
}
