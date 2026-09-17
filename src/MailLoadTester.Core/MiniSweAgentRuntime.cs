using System.Diagnostics;

namespace MailLoadTester.Core;

public enum AiAgentNetworkPolicy { Denied, Isolated, Authorized }

public sealed record AiAgentRuntimeCapabilities(bool IsolatedWorkspace, bool FilesystemConstrained, AiAgentNetworkPolicy NetworkPolicy, bool EnvironmentAllowListed, bool ProcessTreeCancellation);

public sealed record MiniSweAgentLaunchSpec(string WorkspacePath, string TaskPrompt, string Repository, string ImmutableCommit, IReadOnlyList<string> AllowedScopes,
    TimeSpan TimeBudget, int MaxIterations, IReadOnlyDictionary<string, string> Environment, AiAgentNetworkPolicy NetworkPolicy, string? ConfigPath = null, string? Model = null);

public sealed record MiniSweAgentRunResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration);

public interface IMiniSweAgentSandbox
{
    AiAgentRuntimeCapabilities Capabilities { get; }
    Task<MiniSweAgentRunResult> RunAsync(MiniSweAgentLaunchSpec specification, CancellationToken cancellationToken);
}

public sealed class MiniSweAgentRuntime
{
    private readonly IMiniSweAgentSandbox _sandbox;
    private readonly string? _configPath;
    private readonly string? _model;

    public MiniSweAgentRuntime(IMiniSweAgentSandbox sandbox, string? configPath = null, string? model = null)
    { _sandbox = sandbox ?? throw new ArgumentNullException(nameof(sandbox)); _configPath = string.IsNullOrWhiteSpace(configPath) ? null : configPath; _model = string.IsNullOrWhiteSpace(model) ? null : model; }

    public async Task<MiniSweAgentRunResult> RunAsync(AiAgentTask task, string workspacePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task); ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ValidateWorkspace(workspacePath); ValidateCapabilities(_sandbox.Capabilities, task); cancellationToken.ThrowIfCancellationRequested();
        var specification = new MiniSweAgentLaunchSpec(Path.GetFullPath(workspacePath), BuildPrompt(task), task.Repository, task.Commit, task.AllowedScopes, task.TimeBudget, task.MaxIterations,
            new Dictionary<string, string>(StringComparer.Ordinal), AiAgentNetworkPolicy.Denied, _configPath, _model);
        var result = await _sandbox.RunAsync(specification, cancellationToken).ConfigureAwait(false);
        return result with { StandardOutput = SecretRedactor.Redact(result.StandardOutput), StandardError = SecretRedactor.Redact(result.StandardError) };
    }

    private static void ValidateWorkspace(string workspacePath)
    {
        var fullPath = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException($"Agent workspace does not exist: {fullPath}");
        if (Path.GetPathRoot(fullPath) == fullPath) throw new ArgumentException("The agent workspace must not be a filesystem root.", nameof(workspacePath));
    }

    private static void ValidateCapabilities(AiAgentRuntimeCapabilities capabilities, AiAgentTask task)
    {
        if (!capabilities.IsolatedWorkspace) throw new InvalidOperationException("Agent execution requires an isolated workspace capability.");
        if (!capabilities.FilesystemConstrained) throw new InvalidOperationException("Agent execution requires a filesystem-constrained execution boundary.");
        if (!capabilities.EnvironmentAllowListed) throw new InvalidOperationException("Agent execution requires an environment allow-list.");
        if (!capabilities.ProcessTreeCancellation) throw new InvalidOperationException("Agent execution requires process-tree cancellation support.");
        if (capabilities.NetworkPolicy != AiAgentNetworkPolicy.Denied && !task.RealTargetRequired) throw new InvalidOperationException("Non-target agent tasks must run with network access denied.");
    }

    private static string BuildPrompt(AiAgentTask task) => $"Task: {task.TaskId}\nRole: {task.AgentRole}\nRepository: {task.Repository}\nImmutable commit: {task.Commit}\n" +
        $"Allowed scopes:\n- {string.Join("\n- ", task.AllowedScopes)}\nAcceptance criteria:\n- {string.Join("\n- ", task.AcceptanceCriteria)}\nMaximum iterations: {task.MaxIterations}\n" +
        "Work only inside the supplied workspace and only within the allowed scopes. Do not access real targets unless the task is explicitly authorized. Do not expose, copy or print credentials or secrets. Report commands and results accurately.";
}

internal static class SecretRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var lines = value.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var separator = lines[i].IndexOf('=');
            if (separator > 0 && IsSensitiveKey(lines[i][..separator].Trim())) lines[i] = lines[i][..(separator + 1)] + "[REDACTED]";
        }
        return string.Join('\n', lines);
    }
    private static bool IsSensitiveKey(string key) => key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) || key.Contains("API_KEY", StringComparison.OrdinalIgnoreCase) || key.Equals("AUTHORIZATION", StringComparison.OrdinalIgnoreCase);
}
