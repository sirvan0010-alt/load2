using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AutonomousAgentRepairLoopTests
{
    [Fact]
    public async Task FailedAttemptIsRetriedWithFeedbackAndStopsAtAcceptance()
    {
        var backend = new SequenceBackend(
            new AgentExecutionResult(AgentExecutionStatus.Failed, "task-1", null,
                Array.Empty<string>(), new[] { "dotnet test" }, Array.Empty<string>(),
                new[] { "test failure: expected 1 actual 0" }, false, false, 1, TimeSpan.Zero),
            new AgentExecutionResult(AgentExecutionStatus.Completed, "task-1", "abc",
                new[] { "src/Fix.cs" }, new[] { "dotnet test" }, new[] { "tests passed" },
                Array.Empty<string>(), true, true, 1, TimeSpan.Zero));

        var loop = new AutonomousAgentLoop(backend, Policy());
        var result = await loop.ExecuteAsync(Request(maxIterations: 3));

        Assert.Equal(AgentExecutionStatus.Completed, result.Status);
        Assert.Equal(2, result.Iterations);
        Assert.True(result.TestsPassed);
        Assert.Contains(backend.Requests.Skip(1), request =>
            request.RepairFeedback?.Contains("test failure", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task RepairLoopDoesNotExceedIterationBudget()
    {
        var backend = new SequenceBackend(Enumerable.Repeat(
            new AgentExecutionResult(AgentExecutionStatus.Failed, "task-1", null,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                new[] { "still failing" }, false, false, 1, TimeSpan.Zero), 5).ToArray());

        var loop = new AutonomousAgentLoop(backend, Policy(maxIterations: 2));
        var result = await loop.ExecuteAsync(Request(maxIterations: 2));

        Assert.Equal(AgentExecutionStatus.Failed, result.Status);
        Assert.Equal(2, result.Iterations);
        Assert.Equal(2, backend.Requests.Count);
    }

    private static AgentExecutionRequest Request(int maxIterations = 2) => new(
        "task-1", "IMPLEMENTATION_AGENT", "sirvan0010-alt/load2", new string('a', 40),
        Directory.GetCurrentDirectory(), new[] { "src/" }, new[] { "tests pass" },
        TimeSpan.FromSeconds(10), maxIterations);

    private static AgentExecutionPolicy Policy(int maxIterations = 3) => new(
        TimeSpan.FromSeconds(30), maxIterations, 10, false, true, true, true, false);

    private sealed class SequenceBackend : IAgentExecutionBackend
    {
        private readonly Queue<AgentExecutionResult> _results;
        public List<AgentExecutionRequest> Requests { get; } = new();
        public string Name => "test-sequence";

        public SequenceBackend(params AgentExecutionResult[] results) => _results = new(results);

        public Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_results.Count == 0
                ? throw new InvalidOperationException("No test result configured.")
                : _results.Dequeue());
        }
    }
}
