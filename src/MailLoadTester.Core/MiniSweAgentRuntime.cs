using System.Diagnostics;

namespace MailLoadTester.Core;

/// <summary>
/// Launch specification for the external mini-SWE-agent runtime.
/// The runtime is deliberately kept outside the load2 process and must be launched
/// through an explicitly approved sandbox implementation.
/// </summary>
public sealed record MiniSweAgentLaunchSpec(
    string WorkspacePath,
    string TaskPrompt,
    TimeSpan TimeBudget,
    string? ConfigPath = null,
    string? Model = null);

public sealed record MiniSweAgentRunResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);

public interface IMiniSweAgentSandbox
{
    Task<MiniSweAgentRunResult> RunAsync(
        MiniSweAgentLaunchSpec specification,
        CancellationToken cancellationToken);
}

/// <summary>
/// Adapter boundary for mini-SWE-agent v2. It does not execute the process itself.
/// A sandbox implementation owns process isolation, environment allow-listing and
/// filesystem/network policy. This prevents the core library from accidentally
/// turning an agent task into unrestricted shell execution.
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
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = BuildPrompt(task);
        var specification = new MiniSweAgentLaunchSpec(
            Path.GetFullPath(workspacePath),
            prompt,
            task.TimeBudget);

        return await _sandbox.RunAsync(specification, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidateWorkspace(string workspacePath)
    {
        var fullPath = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Agent workspace does not exist: {fullPath}");

        if (Path.GetPathRoot(fullPath) == fullPath)
            throw new ArgumentException("The agent workspace must not be a filesystem root.", nameof(workspacePath));
    }

    private static string BuildPrompt(AiAgentTask task) =>
        $"Task: {task.TaskId}\n" +
        $"Role: {task.AgentRole}\n" +
        $"Repository: {task.Repository}\n" +
        $"Immutable commit: {task.Commit}\n" +
        $"Allowed scopes:\n- {string.Join("\n- ", task.AllowedScopes)}\n" +
        $"Acceptance criteria:\n- {string.Join("\n- ", task.AcceptanceCriteria)}\n" +
        "Work only inside the supplied workspace. Do not access real targets. " +
        "Do not expose, copy or print credentials or secrets. Report commands and results accurately.";
}

/// <summary>
/// Process-backed sandbox implementation. It is intentionally opt-in and requires
/// the caller to supply the executable path and an environment allow-list.
/// Network isolation must be provided by the host/container boundary; this class
/// does not claim to provide OS-level sandboxing by itself.
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
        _environment = environment ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

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

        var start = Stopwatch.GetTimestamp();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(specification.TimeBudget);

        var arguments = new List<string> { "--yolo", "--task", specification.TaskPrompt };
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
                stdout,
                stderr,
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
