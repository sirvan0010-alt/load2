using System.Reflection;
using System.Runtime.Loader;

namespace MailLoadTester;

/// <summary>
/// Discovers trusted local plugin assemblies from the optional "plugins" folder
/// next to the application. Discovery failures are isolated so a broken optional
/// plugin cannot prevent the core tester from starting.
/// </summary>
public static class MailPayloadPluginLoader
{
    public static IReadOnlyList<IMailPayloadPlugin> LoadFromDefaultDirectory(
        Action<string>? log = null)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "plugins");
        return LoadFromDirectory(directory, log);
    }

    public static IReadOnlyList<IMailPayloadPlugin> LoadFromDirectory(
        string directory,
        Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
            return Array.Empty<IMailPayloadPlugin>();

        try
        {
            PathSecurity.EnsureNoReparsePoints(directory);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            log?.Invoke($"Plugin directory rejected: {ex.Message}");
            return Array.Empty<IMailPayloadPlugin>();
        }

        var plugins = new List<IMailPayloadPlugin>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
                     .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                PathSecurity.EnsureNoReparsePoints(path);
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
                foreach (var type in GetPluginTypes(assembly))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IMailPayloadPlugin plugin)
                            plugins.Add(plugin);
                    }
                    catch (Exception ex)
                    {
                        log?.Invoke($"Plugin '{type.FullName}' skipped: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Plugin assembly '{Path.GetFileName(path)}' skipped: {ex.Message}");
            }
        }

        return plugins
            .OrderBy(static p => p.Order)
            .ThenBy(static p => p.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<Type> GetPluginTypes(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(static t => t is not null).Cast<Type>().ToArray();
        }

        return types
            .Where(static t => t is { IsClass: true, IsAbstract: false } &&
                               typeof(IMailPayloadPlugin).IsAssignableFrom(t) &&
                               t.GetConstructor(Type.EmptyTypes) is not null);
    }
}