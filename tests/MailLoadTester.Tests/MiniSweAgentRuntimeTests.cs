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
            var task = CreateTask();
            await runtime.RunAsync(task, workspace);

            Assert.NotNull(sandbox.LastSpecification);
            Assert.Equal(Path.GetFullPath(workspace), sandbox.LastSpecification!.WorkspacePath);
            Assert.Contains(task.Commit, sandbox.LastSpecification.TaskPrompt, StringComparison.Ordinal);
            Assert.Contains("Do not access real targets", sandbox.LastSpecification.TaskPrompt, StringComparison.Ordinal);
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
        var task = CreateTask();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            runtime.RunAsync(task, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

        Assert.Null(sandbox.LastSpecification);
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
                runtime.RunAsync(CreateTask(), workspace, cts.Token));
            Assert.True(sandbox.Called);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static AiAgentTask CreateTask() => new(
        "TEST-RUNTIME-001",
        "IMPLEMENTATION_AGENT",
        "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/MailLoadTester.Core" },
        new[] { "build passes", "tests pass" },
        RealTargetRequired: false,
        Authorized: false,
        TimeSpan.FromSeconds(30),
        MaxIterations: 2);

    private sealed class RecordingSandbox : IMiniSweAgentSandbox
    {
        public MiniSweAgentLaunchSpec? LastSpecification { get; private set; }

        public Task<MiniSweAgentRunResult> RunAsync(
            MiniSweAgentLaunchSpec specification,
            CancellationToken cancellationToken)
        {
            LastSpecification = specification;
            return Task.FromResult(new MiniSweAgentRunResult(0, "ok", string.Empty, TimeSpan.Zero));
        }
    }

    private sealed class CancellingSandbox : IMiniSweAgentSandbox
    {
        public bool Called { get; private set; }

        public Task<MiniSweAgentRunResult> RunAsync(
            MiniSweAgentLaunchSpec specification,
            CancellationToken cancellationToken)
        {
            Called = true;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new MiniSweAgentRunResult(0, string.Empty, string.Empty, TimeSpan.Zero));
        }
    }
}
