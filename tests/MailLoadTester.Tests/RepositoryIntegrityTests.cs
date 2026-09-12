using Xunit;

namespace MailLoadTester.Tests;

public sealed class RepositoryIntegrityTests
{
    [Fact]
    public void Source_files_must_not_contain_artifact_placeholder_markers()
    {
        var root = FindRepositoryRoot();
        var candidates = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(IsAuditableTextFile)
            .Where(path => !IsBuildOutput(path))
            .ToArray();

        Assert.NotEmpty(candidates);

        var offenders = new List<string>();
        foreach (var path in candidates)
        {
            var text = File.ReadAllText(path);
            if (text.Contains("SEE_ARTIFACTS_", StringComparison.Ordinal) ||
                text.Contains("SEE_ARTIFACT", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(root, path));
            }
        }

        Assert.True(offenders.Count == 0,
            "Repository contains unresolved artifact placeholder markers: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void CSharp_source_files_must_not_be_literal_placeholder_files()
    {
        var root = FindRepositoryRoot();
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Where(path =>
            {
                var text = File.ReadAllText(path).Trim();
                return text is "PLACEHOLDER" or "TODO" or "SEE_ARTIFACTS";
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsAuditableTextFile(string path) =>
        path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MailLoadTester.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing MailLoadTester.sln was not found.");
    }
}
