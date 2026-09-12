using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class MailPayloadPluginLoaderSecurityTests
{
    [Fact]
    public void Loader_RejectsPluginDirectoryReparsePoint_WhenSupported()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Directory.CreateTempSubdirectory("mlt-plugin-root-");
        var target = Directory.CreateTempSubdirectory("mlt-plugin-target-");
        var link = Path.Combine(root.FullName, "plugins");
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

            var logs = new List<string>();
            var plugins = MailPayloadPluginLoader.LoadFromDirectory(link, logs.Add);

            Assert.Empty(plugins);
            Assert.Contains(logs, static message =>
                message.Contains("reparse", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("symlink", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("junction", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(link, false); } catch { }
            root.Delete(true);
            target.Delete(true);
        }
    }

    [Fact]
    public void Loader_SkipsPluginAssemblyReparsePoint_WhenSupported()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = Directory.CreateTempSubdirectory("mlt-plugin-files-");
        var target = Directory.CreateTempSubdirectory("mlt-plugin-file-target-");
        var source = Path.Combine(target.FullName, "plugin.dll");
        var link = Path.Combine(directory.FullName, "plugin.dll");
        try
        {
            File.WriteAllBytes(source, new byte[] { 0x4D, 0x5A, 0x00, 0x01 });
            try
            {
                File.CreateSymbolicLink(link, source);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            var logs = new List<string>();
            var plugins = MailPayloadPluginLoader.LoadFromDirectory(directory.FullName, logs.Add);

            Assert.Empty(plugins);
            Assert.Contains(logs, static message =>
                message.Contains("reparse", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("symlink", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("junction", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(link); } catch { }
            directory.Delete(true);
            target.Delete(true);
        }
    }
}