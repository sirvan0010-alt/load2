using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class MiniSweAgentRuntimeTests
{
    [Fact]
    public async Task PassesImmutableTaskAndNormalizedWorkspaceToSandbox()
    {
        var sandbox = new RecordingSandbox();
        var runtime = new MiniSweAgentRuntime(sandbox);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-agent-test-" + Guid.NewGuid())).FullName;
        try
        {
            var task = CreateTask(workspace);
            await runtime.RunAsync(task, workspace);

            Assert.NotNull(sandbox.LastSpecification);
            Assert.Equal(Path.GetFullPath(workspace), sandbox.LastSpecification!.WorkspacePath);
            Assert.Equal(task.Repository, sandbox.LastSpecification.Repository);
            Assert.Equal(task.Commit, sandbox.LastSpecification.ImmutableCommit);
            Assert.Equal(task.AllowedScopes, sandbox.LastSpecification.AllowedScopes);
            Assert.Equal(task.MaxIterations, sandbox.LastSpecification.MaxIterations);
            Assert.Contains(task.Commit, sandbox.LastSpecification.TaskPrompt, StringComparison.Ordinal);
            Assert.Contains("Do not access real targets", sandbox.LastSpecification.TaskPrompt, StringComparison.Ordinal);
            Assert.Equal(AiAgentNetworkPolicy.Denied, sandbox.LastSpecification.NetworkPolicy);
            Assert.Empty(sandbox.LastSpecification.Environment);
            Assert.Equal(task.TimeBudget, sandbox.LastSpecification.TimeBudget);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsMissingWorkspaceBeforeSandboxExecution()
    {
        var sandbox = new RecordingSandbox();
        var runtime = new MiniSweAgentRuntime(sandbox);
        var task = CreateTask(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            runtime.RunAsync(task, task.WorkspacePath));

        Assert.Null(sandbox.LastSpecification);
    }

    [Fact]
    public async Task RejectsExecutionBoundaryWithoutIsolation()
    {
        var sandbox = new UnconfinedSandbox();
        var runtime = new MiniSweAgentRuntime(sandbox);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-agent-test-" + Guid.NewGuid())).FullName;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runtime.RunAsync(CreateTask(workspace), workspace));
            Assert.Null(sandbox.LastSpecification);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationIsPropagatedToSandbox()
    {
        var sandbox = new CancellingSandbox();
        var runtime = new MiniSweAgentRuntime(sandbox);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-agent-test-" + Guid.NewGuid())).FullName;
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                runtime.RunAsync(CreateTask(workspace), workspace, cts.Token));
            Assert.False(sandbox.Called);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RedactsSensitiveKeyValueLinesFromRuntimeOutput()
    {
        var sandbox = new OutputSandbox();
        var runtime = new MiniSweAgentRuntime(sandbox);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-agent-test-" + Guid.NewGuid())).FullName;
        try
        {
            var result = await runtime.RunAsync(CreateTask(workspace), workspace);

            Assert.Contains("PASSWORD=[REDACTED]", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("super-secret", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("normal=value", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static AiAgentTask CreateTask(string workspacePath) => new(
        "TEST-RUNTIME-001",
        "IMPLEMENTATION_AGENT",
        "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/MailLoadTester.Core" },
        new[] { "build passes", "tests pass" },
        RealTargetRequired: false,
        Authorized: false,
        TimeSpan.FromSeconds(30),
        MaxIterations: 2,
        workspacePath);

    private static readonly AiAgentRuntimeCapabilities SafeCapabilities = new(
        IsolatedWorkspace: true,
        FilesystemConstrained: true,
        NetworkPolicy: AiAgentNetworkPolicy.Denied,
        EnvironmentAllowListed: true,
        ProcessTreeCancellation: true);

    private sealed class RecordingSandbox : IMiniSweAgentSandbox
    {
        public MiniSweAgentLaunchSpec? LastSpecification { get; private set; }
        public AiAgentRuntimeCapabilities Capabilities => SafeCapabilities;

        public Task<MiniSweAgentRunResult> RunAsync(
            MiniSweAgentLaunchSpec specification,
            CancellationToken cancellationToken)
        {
            LastSpecification = specification;
            return Task.FromResult(new MiniSweAgentRunResult(0, "ok", string.Empty, TimeSpan.Zero));
        }
    }

    private sealed class UnconfinedSandbox : IMiniSweAgentSandbox
    {
        public AiAgentRuntimeCapabilities Capabilities => new(
            IsolatedWorkspace: false,
            FilesystemConstrained: false,
            NetworkPolicy: AiAgentNetworkPolicy.Denied,
            EnvironmentAllowListed: true,
            ProcessTreeCancellation: true);

        public Task<MiniSweAgentRunResult> RunAsync(
            MiniSweAgentLaunchSpec specification,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should not execute.");
    }

    private sealed class CancellingSandbox : IMiniSweAgentSandbox
    {
        public bool Called { get; private set; }
        public AiAgentRuntimeCapabilities Capabilities => SafeCapabilities;

        public Task<MiniSweAgentRunResult> RunAsync(
            MiniSweAgentLaunchSpec specification,
            CancellationToken cancellationToken)
        {
            Called = true;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new MiniSweAgentRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));
        }
    }

    private sealed class OutputSandbox : IMiniSweAgentSandbox
    {
        public AiAgentRuntimeCapabilities Capabilities => SafeCapabilities;

        public Task<MiniSweAgentRunResult> RunAsync(
            MiniSweAgentLaunchSpec specification,
            CancellationToken cancellationToken) =>
            Task.FromResult(new MiniSweAgentRunResult(
                0,
                "PASSWORD=super-secret\nnormal=value",
                "TOKEN=another-secret",
                TimeSpan.FromMilliseconds(1)));
    }
}
