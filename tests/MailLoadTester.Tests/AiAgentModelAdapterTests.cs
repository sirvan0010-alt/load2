using Xunit;

namespace MailLoadTester.Tests;

public sealed class AiAgentModelAdapterTests
{
    [Fact]
    public async Task ModelBackedAgent_TreatsModelOutputAsUnverifiedEvidence()
    {
        var adapter = new StubModelAdapter(new AiAgentModelResponse(
            "Inspect and propose a bounded change.",
            new[] { "src/MailLoadTester.Core/Example.cs" },
            new[] { "dotnet test" },
            new[] { "model-observation" },
            RequiresIndependentVerification: false));

        var agent = new ModelBackedAiAgent(adapter);
        var task = new AiAgentTask(
            "T-001",
            "developer",
            "sirvan0010-alt/load2",
            new string('a', 40),
            new[] { "src/MailLoadTester.Core" },
            new[] { "tests pass" },
            RealTargetRequired: false,
            Authorized: false,
            TimeSpan.FromMinutes(1),
            1,
            Environment.CurrentDirectory);

        var result = await agent.ExecuteAsync(
            new AiAgentContext(
                task,
                CancellationToken.None,
                DateTimeOffset.UtcNow.AddMinutes(1),
                1),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
        Assert.True(result.CiRequired);
        Assert.Contains(result.Findings, x => x.EvidenceLevel == AgentEvidenceLevel.SourceDocumented);
        Assert.Contains("unverified evidence only", result.Handoff);
    }

    [Fact]
    public async Task ModelBackedAgent_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var adapter = new StubModelAdapter(new AiAgentModelResponse(
            "unused",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            true));

        var agent = new ModelBackedAiAgent(adapter);
        var task = new AiAgentTask(
            "T-002",
            "developer",
            "sirvan0010-alt/load2",
            new string('b', 40),
            new[] { "src" },
            new[] { "tests pass" },
            false,
            false,
            TimeSpan.FromMinutes(1),
            1,
            Environment.CurrentDirectory);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            agent.ExecuteAsync(
                new AiAgentContext(task, cts.Token, DateTimeOffset.UtcNow.AddMinutes(1), 1),
                cts.Token));
    }

    private sealed class StubModelAdapter : IAiAgentModelAdapter
    {
        private readonly AiAgentModelResponse _response;

        public StubModelAdapter(AiAgentModelResponse response) => _response = response;

        public Task<AiAgentModelResponse> GenerateAsync(
            AiAgentModelRequest request,
            CancellationToken cancellationToken)
        {
            Assert.Equal("sirvan0010-alt/load2", request.Repository);
            Assert.Equal(40, request.ImmutableCommit.Length);
            return Task.FromResult(_response);
        }
    }
}
