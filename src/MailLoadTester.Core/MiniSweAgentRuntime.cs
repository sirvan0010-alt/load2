using System.Diagnostics;

namespace MailLoadTester.Core;

public enum AiAgentNetworkPolicy
{
    Denied,
    Isolated,
    Authorized
}

public sealed record AiAgentRuntimeCapabilities(
    bool IsolatedWorkspace,
    bool FilesystemConstrained,
    AiAgentNetworkPolicy NetworkPolicy,
    bool EnvironmentAllowListed,
    bool ProcessTreeCancellation);

/// <summary>
/// Immutable launch contract for the external mini-SWE-agent runtime.
/// </summary>
public sealed record MiniSweAgentLaunchSpec(
    string WorkspacePath,
    string TaskPrompt,
    string Repository,
    string ImmutableCommit,
    IReadOnlyList<string> AllowedScopes,
    TimeSpan TimeBudget,
    int MaxIterations,
    IReadOnlyDictionary<string, string> Environment,
    AiAgentNetworkPolicy NetworkPolicy,
    string? ConfigPath = null,
    string? Model = null);

public sealed record MiniSweAgentRunResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);

public interface IMiniSweAgentSandbox
{
    AiAgentRuntimeCapabilities Capabilities { get; }

    Task<MiniSweAgentRunResult> RunAsync(
        MiniSweAgentLaunchSpec specification,
        CancellationToken cancellationToken);
}

/// <summary>
/// Provider-neutral adapter for mini-SWE-agent v2. It owns the immutable task
/// launch contract; the execution boundary owns OS/container isolation.
/// </summary>
public sealed class MiniSweAgentRuntime
{
    private readonly IMiniSweAgentSandbox _sandbox;

    public MiniSweAgentRuntime(IMiniSweAgentSandbox sandbox)
    {
        _sandbox = sandbox ?? throw new ArgumentNullException(nameof(sandbox));
    }

    public async Task<MiniSweAgentRunResult> RunAsync(
        AiAgentTask task,
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        ValidateWorkspace(workspacePath);
        ValidateCapabilities(_sandbox.Capabilities, task);
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = BuildPrompt(task);
        var specification = new MiniSweAgentLaunchSpec(
            Path.GetFullPath(workspacePath),
            prompt,
            task.Repository,
            task.Commit,
            task.AllowedScopes,
            task.TimeBudget,
            task.MaxIterations,
            new Dictionary<string, string>(StringComparer.Ordinal),
            AiAgentNetworkPolicy.Denied);

        var result = await _sandbox.RunAsync(specification, cancellationToken)
            .ConfigureAwait(false);

        return result with
        {
            StandardOutput = SecretRedactor.Redact(result.StandardOutput),
            StandardError = SecretRedactor.Redact(result.StandardError)
        };
    }

    private static void ValidateWorkspace(string workspacePath)
    {
        var fullPath = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Agent workspace does not exist: {fullPath}");

        if (Path.GetPathRoot(fullPath) == fullPath)
            throw new ArgumentException("The agent workspace must not be a filesystem root.", nameof(workspacePath));
    }

    private static void ValidateCapabilities(AiAgentRuntimeCapabilities capabilities, AiAgentTask task)
    {
        if (!capabilities.IsolatedWorkspace)
            throw new InvalidOperationException("Agent execution requires an isolated workspace capability.");
        if (!capabilities.FilesystemConstrained)
            throw new InvalidOperationException("Agent execution requires a filesystem-constrained execution boundary.");
        if (!capabilities.EnvironmentAllowListed)
            throw new InvalidOperationException("Agent execution requires an environment allow-list.");
        if (!capabilities.ProcessTreeCancellation)
            throw new InvalidOperationException("Agent execution requires process-tree cancellation support.");
        if (capabilities.NetworkPolicy != AiAgentNetworkPolicy.Denied && !task.RealTargetRequired)
            throw new InvalidOperationException("Non-target agent tasks must run with network access denied.");
    }

    private static string BuildPrompt(AiAgentTask task) =>
        $"Task: {task.TaskId}\n" +
        $"Role: {task.AgentRole}\n" +
        $"Repository: {task.Repository}\n" +
        $"Immutable commit: {task.Commit}\n" +
        $"Allowed scopes:\n- {string.Join("\n- ", task.AllowedScopes)}\n" +
        $"Acceptance criteria:\n- {string.Join("\n- ", task.AcceptanceCriteria)}\n" +
        $"Maximum iterations: {task.MaxIterations}\n" +
        "Work only inside the supplied workspace and only within the allowed scopes. " +
        "Do not access real targets unless the task is explicitly authorized. " +
        "Do not expose, copy or print credentials or secrets. Report commands and results accurately.";
}

/// <summary>
/// Process-backed execution boundary. It is not an OS sandbox by itself. The host
/// must provide filesystem/network confinement before this implementation is used.
/// The process receives an explicitly supplied environment only; the host environment
/// is cleared to prevent accidental credential inheritance.
/// </summary>
public sealed class ProcessMiniSweAgentSandbox : IMiniSweAgentSandbox
{
    private readonly string _executablePath;
    private readonly IReadOnlyDictionary<string, string> _environment;

    public ProcessMiniSweAgentSandbox(
        string executablePath,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        _executablePath = executablePath;
        _environment = new Dictionary<string, string>(
            environment ?? new Dictionary<string, string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    public AiAgentRuntimeCapabilities Capabilities => new(
        IsolatedWorkspace: false,
        FilesystemConstrained: false,
        NetworkPolicy: AiAgentNetworkPolicy.Denied,
        EnvironmentAllowListed: true,
        ProcessTreeCancellation: true);

    public async Task<MiniSweAgentRunResult> RunAsync(
        MiniSweAgentLaunchSpec specification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specification);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(specification.WorkspacePath))
            throw new DirectoryNotFoundException(specification.WorkspacePath);
        if (specification.TimeBudget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(specification), "TimeBudget must be positive.");
        if (specification.MaxIterations is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(specification), "MaxIterations must be between 1 and 20.");

        var start = Stopwatch.GetTimestamp();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(specification.TimeBudget);

        var arguments = new List<string> { "--task", specification.TaskPrompt };
        if (!string.IsNullOrWhiteSpace(specification.ConfigPath))
        {
            arguments.Add("--config");
            arguments.Add(specification.ConfigPath);
        }
        if (!string.IsNullOrWhiteSpace(specification.Model))
        {
            arguments.Add("--model");
            arguments.Add(specification.Model);
        }

        var psi = new ProcessStartInfo
        {
            FileName = _executablePath,
            WorkingDirectory = specification.WorkspacePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        psi.Environment.Clear();
        foreach (var pair in _environment)
            psi.Environment[pair.Key] = pair.Value;

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start the mini-SWE-agent process.");

        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return new MiniSweAgentRunResult(
                process.ExitCode,
                SecretRedactor.Redact(stdout),
                SecretRedactor.Redact(stderr),
                Stopwatch.GetElapsedTime(start));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and Kill().
        }
    }
}

internal static class SecretRedactor
{
    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var lines = value.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var separator = line.IndexOf('=');
            if (separator > 0 && IsSensitiveKey(line[..separator].Trim()))
                lines[i] = line[..(separator + 1)] + "[REDACTED]";
        }

        return string.Join('\n', lines);
    }

    private static bool IsSensitiveKey(string key) =>
        key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("API_KEY", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("AUTHORIZATION", StringComparison.OrdinalIgnoreCase);
}
