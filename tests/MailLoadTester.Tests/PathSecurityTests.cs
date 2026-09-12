using Xunit;

namespace MailLoadTester.Tests;

public sealed class PathSecurityTests
{
    [Fact]
    public void ExistingRegularFile_IsAllowed()
    {
        var root = Directory.CreateTempSubdirectory("mail-load-path-");
        try
        {
            var file = Path.Combine(root.FullName, "input.txt");
            File.WriteAllText(file, "test");
            PathSecurity.EnsureNoReparsePoints(file);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void MissingFile_AllowsNonReparseParent()
    {
        var root = Directory.CreateTempSubdirectory("mail-load-path-");
        try
        {
            var file = Path.Combine(root.FullName, "not-created.txt");
            PathSecurity.EnsureNoReparsePoints(file);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void ExistingDirectory_ReparsePoint_IsRejected_WhenSupported()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Directory.CreateTempSubdirectory("mail-load-path-");
        var target = Directory.CreateTempSubdirectory("mail-load-target-");
        var link = Path.Combine(root.FullName, "link");
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, target.FullName);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            var input = Path.Combine(link, "file.eml");
            var ex = Assert.Throws<ArgumentException>(() => PathSecurity.EnsureNoReparsePoints(input));
            Assert.Contains("symlink/junction/reparse", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(link, false); } catch { }
            root.Delete(true);
            target.Delete(true);
        }
    }
}
