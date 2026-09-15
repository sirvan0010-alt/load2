using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AutonomousAgentOrchestratorTests
{
    [Fact]
    public async Task StopsAtFirstBlockedSpecialist()
    {
        var backend = new FixedBackend(
            new AgentExecutionResult(AgentExecutionStatus.Completed, "a", null,
                Array.Empty<string>(), Array.Empty<string>(), new[] { "ok" }, Array.Empty<string>(), true, true, 1, TimeSpan.Zero),
            new AgentExecutionResult(AgentExecutionStatus.Blocked, "b", null,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new[] { "blocked" }, false, false, 1, TimeSpan.Zero));

        var loop = new AutonomousAgentLoop(backend, Policy());
        var orchestrator = new AutonomousAgentOrchestrator(loop);
        var result = await orchestrator.ExecuteAsync(new[] { Request("a"), Request("b"), Request("c") });

        Assert.False(result.Completed);
        Assert.Equal(2, result.Results.Count);
        Assert.Contains("blocked", result.Blocker, StringComparison.OrdinalIgnoreCase);
    }

    private static AgentExecutionRequest Request(string id) => new(
        id, "IMPLEMENTATION_AGENT", "sirvan0010-alt/load2", new string('a', 40),
        Directory.GetCurrentDirectory(), new[] { "src/" }, new[] { "tests pass" },
        TimeSpan.FromSeconds(10), 1);

    private static AgentExecutionPolicy Policy() => new(
        TimeSpan.FromSeconds(30), 3, 10, false, true, true, true, false);

    private sealed class FixedBackend : IAgentExecutionBackend
    {
        private readonly Queue<AgentExecutionResult> _results;
        public string Name => "test-fixed";
        public FixedBackend(params AgentExecutionResult[] results) => _results = new(results);

        public Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_results.Dequeue() with { TaskId = request.TaskId });
        }
    }
}
