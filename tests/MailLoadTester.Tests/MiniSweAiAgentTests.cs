using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class MiniSweAiAgentTests
{
    [Fact]
    public async Task ExecutesThroughBoundedRuntimeAndRequiresEvidence()
    {
        var sandbox = new RecordingSandbox();
        var runtime = new MiniSweAgentRuntime(sandbox, "config.yaml", "deterministic");
        var agent = new MiniSweAiAgent(runtime);
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-mswe-" + Guid.NewGuid())).FullName;

        try
        {
            var task = new AiAgentTask(
                "TEST-MSWE-001",
                "IMPLEMENTATION",
                "sirvan0010-alt/load2",
                "0123456789abcdef0123456789abcdef01234567",
                new[] { "src/MailLoadTester.Core" },
                new[] { "tests pass" },
                false,
                false,
                TimeSpan.FromSeconds(5),
                2,
                workspace);

            var context = new AiAgentContext(
                task,
                CancellationToken.None,
                DateTimeOffset.UtcNow.AddSeconds(5),
                1);

            var result = await agent.ExecuteAsync(context, CancellationToken.None);

            Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
            Assert.Equal("0", result.Acceptance["ExitCode"]);
            Assert.Equal(AiAgentNetworkPolicy.Denied, sandbox.Specification!.NetworkPolicy);
            Assert.Equal("config.yaml", sandbox.Specification.ConfigPath);
            Assert.Equal("deterministic", sandbox.Specification.Model);
            Assert.Contains("independent verification", result.Handoff, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    private sealed class RecordingSandbox : IMiniSweAgentSandbox
    {
        public AiAgentRuntimeCapabilities Capabilities =>
            new(true, true, AiAgentNetworkPolicy.Denied, true, true);

        public MiniSweAgentLaunchSpec? Specification { get; private set; }

        public Task<MiniSweAgentRunResult> RunAsync(
            MiniSweAgentLaunchSpec specification,
            CancellationToken cancellationToken)
        {
            Specification = specification;
            return Task.FromResult(new MiniSweAgentRunResult(
                0,
                "done",
                string.Empty,
                TimeSpan.FromMilliseconds(10)));
        }
    }
}