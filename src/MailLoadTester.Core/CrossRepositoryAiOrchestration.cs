namespace MailLoadTester.Core;

public enum AiRepositoryCapability
{
    Inspect,
    Plan,
    Modify,
    Test,
    Verify,
    Document
}

public sealed record AiRepositoryDescriptor(
    string Repository,
    string SourceOfTruthRef,
    string ImmutableCommit,
    IReadOnlySet<AiRepositoryCapability> Capabilities,
    IReadOnlySet<string> AllowedScopes);

public sealed record AiRepositoryTask(
    string TaskId,
    string Repository,
    string RequestedChange,
    IReadOnlyList<string> RequiredCapabilities,
    bool NetworkRequired = false);

public sealed record AiRepositoryAction(
    string ActionId,
    string Repository,
    AiRepositoryCapability Capability,
    string Scope,
    string Description);

public interface IAiRepositoryRegistry
{
    AiRepositoryDescriptor GetRequired(string repository);
    IReadOnlyCollection<AiRepositoryDescriptor> Repositories { get; }
}

public sealed class AiRepositoryRegistry : IAiRepositoryRegistry
{
    private readonly IReadOnlyDictionary<string, AiRepositoryDescriptor> _repositories;

    public AiRepositoryRegistry(IEnumerable<AiRepositoryDescriptor> repositories)
    {
        ArgumentNullException.ThrowIfNull(repositories);

        var map = new Dictionary<string, AiRepositoryDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var repository in repositories)
        {
            ArgumentNullException.ThrowIfNull(repository);

            if (string.IsNullOrWhiteSpace(repository.Repository))
                throw new ArgumentException("Repository is required.", nameof(repositories));

            if (!map.TryAdd(repository.Repository, repository))
                throw new ArgumentException($"Repository '{repository.Repository}' is registered more than once.", nameof(repositories));

            ValidateDescriptor(repository);
        }

        _repositories = map;
    }

    public IReadOnlyCollection<AiRepositoryDescriptor> Repositories => _repositories.Values.ToArray();

    public AiRepositoryDescriptor GetRequired(string repository)
    {
        if (!_repositories.TryGetValue(repository, out var descriptor))
            throw new KeyNotFoundException($"Repository '{repository}' is not registered.");
        return descriptor;
    }

    private static void ValidateDescriptor(AiRepositoryDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.SourceOfTruthRef))
            throw new ArgumentException($"Repository '{descriptor.Repository}' has no source-of-truth ref.");

        if (descriptor.ImmutableCommit.Length != 40 || !descriptor.ImmutableCommit.All(Uri.IsHexDigit))
            throw new ArgumentException($"Repository '{descriptor.Repository}' must use a 40-character immutable commit SHA.");

        if (descriptor.Capabilities.Count == 0)
            throw new ArgumentException($"Repository '{descriptor.Repository}' has no capabilities.");

        if (descriptor.AllowedScopes.Count == 0)
            throw new ArgumentException($"Repository '{descriptor.Repository}' has no allowed scopes.");

        foreach (var scope in descriptor.AllowedScopes)
            ValidateScope(scope);
    }

    internal static void ValidateScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Repository scope cannot be empty.");

        var normalized = scope.Replace('\\', '/').Trim();
        if (Path.IsPathRooted(scope) ||
            normalized == ".." ||
            normalized.StartsWith("../", StringComparison.Ordinal) ||
            normalized.Contains("/../", StringComparison.Ordinal) ||
            normalized.EndsWith("/..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Repository scope escapes its workspace: {scope}");
        }
    }
}

public sealed class CrossRepositoryAiSupervisor
{
    private readonly IAiRepositoryRegistry _registry;

    public CrossRepositoryAiSupervisor(IAiRepositoryRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IReadOnlyList<AiRepositoryAction> Authorize(
        AiRepositoryTask task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(task.TaskId))
            throw new ArgumentException("TaskId is required.", nameof(task));

        if (string.IsNullOrWhiteSpace(task.RequestedChange))
            throw new ArgumentException("RequestedChange is required.", nameof(task));

        var repository = _registry.GetRequired(task.Repository);

        if (task.NetworkRequired)
            throw new InvalidOperationException(
                "Cross-repository autonomous execution cannot enable network access through this policy layer.");

        var actions = new List<AiRepositoryAction>();
        foreach (var capabilityName in task.RequiredCapabilities)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Enum.TryParse<AiRepositoryCapability>(capabilityName, true, out var capability))
                throw new InvalidOperationException($"Unknown repository capability '{capabilityName}'.");

            if (!repository.Capabilities.Contains(capability))
                throw new InvalidOperationException(
                    $"Repository '{repository.Repository}' does not allow capability '{capability}'.");

            var scope = repository.AllowedScopes.First();
            actions.Add(new AiRepositoryAction(
                $"{task.TaskId}-{capability.ToString().ToLowerInvariant()}",
                repository.Repository,
                capability,
                scope,
                task.RequestedChange));
        }

        return actions;
    }
}

public sealed record AiRepositoryHandoff(
    string TaskId,
    string Repository,
    string ImmutableCommit,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string> Tests,
    IReadOnlyList<string> Findings,
    bool IndependentlyVerified,
    string NextStep);
