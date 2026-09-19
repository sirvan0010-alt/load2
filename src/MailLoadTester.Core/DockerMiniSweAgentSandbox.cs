using System.Diagnostics;

namespace MailLoadTester.Core;

public sealed class DockerMiniSweAgentSandbox : IMiniSweAgentSandbox
{
    private readonly string _dockerExecutable;
    private readonly string _image;
    private readonly IReadOnlyDictionary<string, string> _environment;
    private readonly long _memoryBytes;
    private readonly int _cpuLimit;
    private readonly int _pidsLimit;

    public DockerMiniSweAgentSandbox(string image, IReadOnlyDictionary<string, string>? environment = null, string dockerExecutable = "docker",
        long memoryBytes = 2L * 1024 * 1024 * 1024, int cpuLimit = 2, int pidsLimit = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(image); ArgumentException.ThrowIfNullOrWhiteSpace(dockerExecutable);
        if (memoryBytes <= 0) throw new ArgumentOutOfRangeException(nameof(memoryBytes));
        if (cpuLimit < 1) throw new ArgumentOutOfRangeException(nameof(cpuLimit));
        if (pidsLimit < 1) throw new ArgumentOutOfRangeException(nameof(pidsLimit));
        _image = image; _dockerExecutable = dockerExecutable; _environment = new Dictionary<string, string>(environment ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
        _memoryBytes = memoryBytes; _cpuLimit = cpuLimit; _pidsLimit = pidsLimit;
    }

    public AiAgentRuntimeCapabilities Capabilities => new(true, true, AiAgentNetworkPolicy.Denied, true, true);

    public async Task<MiniSweAgentRunResult> RunAsync(MiniSweAgentLaunchSpec specification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specification); cancellationToken.ThrowIfCancellationRequested();
        var workspace = Path.GetFullPath(specification.WorkspacePath);
        if (!Directory.Exists(workspace)) throw new DirectoryNotFoundException(workspace);
        if (Path.GetPathRoot(workspace) == workspace) throw new ArgumentException("The agent workspace must not be a filesystem root.", nameof(specification));
        if (specification.NetworkPolicy != AiAgentNetworkPolicy.Denied) throw new InvalidOperationException("Docker agent execution requires network access to be denied.");
        if (specification.TimeBudget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(specification), "TimeBudget must be positive.");
        if (specification.MaxIterations is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(specification), "MaxIterations must be between 1 and 20.");

        var start = Stopwatch.GetTimestamp();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(specification.TimeBudget);
        var psi = new ProcessStartInfo { FileName = _dockerExecutable, WorkingDirectory = workspace, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in BuildDockerArguments(specification, workspace)) psi.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = psi };
        if (!process.Start()) throw new InvalidOperationException("Failed to start Docker agent execution.");
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token); var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            return new MiniSweAgentRunResult(process.ExitCode, SecretRedactor.Redact(await stdoutTask.ConfigureAwait(false)), SecretRedactor.Redact(await stderrTask.ConfigureAwait(false)), Stopwatch.GetElapsedTime(start));
        }
        catch (OperationCanceledException) { TryKill(process); throw; }
    }

    internal List<string> BuildDockerArguments(MiniSweAgentLaunchSpec specification, string workspace)
    {
        var arguments = new List<string> { "run", "--rm", "--network", "none", "--read-only", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
            "--pids-limit", _pidsLimit.ToString(System.Globalization.CultureInfo.InvariantCulture), "--memory", $"{_memoryBytes}b", "--cpus", _cpuLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--tmpfs", "/tmp:rw,noexec,nosuid,size=256m", "--mount", $"type=bind,src={workspace},dst=/workspace", "--workdir", "/workspace" };
        foreach (var pair in _environment)
        {
            if (IsCredentialKey(pair.Key)) throw new ArgumentException($"Credential-like environment key is not permitted: {pair.Key}", nameof(specification));
            arguments.Add("--env"); arguments.Add($"{pair.Key}={pair.Value}");
        }
        arguments.Add(_image); arguments.Add("--task"); arguments.Add(specification.TaskPrompt);
        if (!string.IsNullOrWhiteSpace(specification.ConfigPath)) { arguments.Add("--config"); arguments.Add(specification.ConfigPath); }
        if (!string.IsNullOrWhiteSpace(specification.Model)) { arguments.Add("--model"); arguments.Add(specification.Model); }
        return arguments;
    }

    private static bool IsCredentialKey(string key) => key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase) || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) || key.Contains("API_KEY", StringComparison.OrdinalIgnoreCase) || key.Equals("AUTHORIZATION", StringComparison.OrdinalIgnoreCase);
    private static void TryKill(Process process) { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
}
