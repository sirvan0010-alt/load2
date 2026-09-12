namespace MailLoadTester;

/// <summary>
/// Filesystem safety checks for inputs that are consumed or created by the tester.
/// A path containing '..' is handled by Validation; this class additionally rejects
/// Windows reparse points (symlinks/junctions) anywhere in the existing path chain.
/// This prevents a seemingly local path from resolving through a link to an
/// unintended filesystem location.
/// </summary>
public static class PathSecurity
{
    public static void EnsureNoReparsePoints(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Cesta nesmí být prázdná.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        var current = File.Exists(fullPath) || Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? fullPath;

        while (!string.IsNullOrEmpty(current))
        {
            if (IsReparsePoint(current))
                throw new ArgumentException($"Cesta nesmí procházet symlinkem/junction/reparse pointem: {path}", nameof(path));

            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                break;
            current = parent ?? string.Empty;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}
