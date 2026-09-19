using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class MiniSweAgentRuntimeTests
{
    [Fact]
    public async Task RuntimeAlwaysLaunchesSandboxWithNetworkDenied()
    {
        var sandbox = new RecordingSandbox(new AiAgentRuntimeCapabilities(true, true, AiAgentNetworkPolicy.Denied, true, true));
        var runtime = new MiniSweAgentRuntime(sandbox);
        var task = TaskFor();

        var result = await runtime.RunAsync(task, Path.GetTempPath(), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(sandbox.Specification);
        Assert.Equal(AiAgentNetworkPolicy.Denied, sandbox.Specification!.NetworkPolicy);
        Assert.Empty(sandbox.Specification.Environment);
        Assert.Contains(task.Commit, sandbox.Specification.TaskPrompt, StringComparison.Ordinal);
        Assert.Contains("src/MailLoadTester.Core", sandbox.Specification.TaskPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeRejectsSandboxWithoutRequiredIsolation()
    {
        var sandbox = new RecordingSandbox(new AiAgentRuntimeCapabilities(false, true, AiAgentNetworkPolicy.Denied, true, true));
        var runtime = new MiniSweAgentRuntime(sandbox);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.RunAsync(TaskFor(), Path.GetTempPath(), CancellationToken.None));

        Assert.Null(sandbox.Specification);
    }

    [Fact]
    public async Task RuntimeRejectsSandboxWithNonDeniedNetwork()
    {
        var sandbox = new RecordingSandbox(new AiAgentRuntimeCapabilities(true, true, AiAgentNetworkPolicy.Authorized, true, true));
        var runtime = new MiniSweAgentRuntime(sandbox);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.RunAsync(TaskFor(), Path.GetTempPath(), CancellationToken.None));
    }

    private static AiAgentTask TaskFor() => new(
        "RUNTIME-001",
        "TEST_AGENT",
        "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/MailLoadTester.Core" },
        new[] { "tests pass" },
        false,
        false,
        TimeSpan.FromSeconds(5),
        2,
        Path.GetTempPath());

    private sealed class RecordingSandbox : IMiniSweAgentSandbox
    {
        public AiAgentRuntimeCapabilities Capabilities { get; }
        public MiniSweAgentLaunchSpec? Specification { get; private set; }

        public RecordingSandbox(AiAgentRuntimeCapabilities capabilities) => Capabilities = capabilities;

        public Task<MiniSweAgentRunResult> RunAsync(MiniSweAgentLaunchSpec specification, CancellationToken cancellationToken)
        {
            Specification = specification;
            return Task.FromResult(new MiniSweAgentRunResult(0, "ok", "", TimeSpan.Zero));
        }
    }
}
