using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AutonomousAgentContractsTests
{
    [Fact]
    public void PolicyValidator_RejectsMissingAcceptanceCriteria()
    {
        var request = CreateRequest() with { AcceptanceCriteria = Array.Empty<string>() };
        var policy = CreatePolicy();

        Assert.Throws<ArgumentException>(() => new AgentExecutionPolicyValidator().Validate(request, policy));
    }

    [Fact]
    public void PolicyValidator_RejectsIterationBudgetAbovePolicy()
    {
        var request = CreateRequest() with { MaxIterations = 21 };
        var policy = CreatePolicy();

        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentExecutionPolicyValidator().Validate(request, policy));
    }

    [Fact]
    public void PolicyValidator_RejectsUnauthorizedNetworkRequirement()
    {
        var request = CreateRequest() with { RealTargetRequired = true };
        var policy = CreatePolicy() with { AllowNetworkAccess = false };

        Assert.Throws<InvalidOperationException>(() => new AgentExecutionPolicyValidator().Validate(request, policy));
    }

    [Fact]
    public async Task AutonomousLoop_DelegatesOnlyAfterValidation()
    {
        var backend = new RecordingBackend();
        var loop = new AutonomousAgentLoop(backend, CreatePolicy());

        var result = await loop.ExecuteAsync(CreateRequest());

        Assert.Equal(AgentExecutionStatus.Completed, result.Status);
        Assert.Equal(1, backend.Calls);
    }

    private static AgentExecutionRequest CreateRequest() => new(
        "task-001", "IMPLEMENTATION", "sirvan0010-alt/load2", "0123456789abcdef",
        Directory.GetCurrentDirectory(), new[] { "src/**" }, new[] { "tests pass" },
        TimeSpan.FromMinutes(5), 3);

    private static AgentExecutionPolicy CreatePolicy() => new(
        TimeSpan.FromMinutes(10), 20, 50, true, true, true, true, false);

    private sealed class RecordingBackend : IAgentExecutionBackend
    {
        public int Calls { get; private set; }
        public string Name => "test";

        public Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new AgentExecutionResult(
                AgentExecutionStatus.Completed, request.TaskId, request.ImmutableCommit,
                Array.Empty<string>(), Array.Empty<string>(), new[] { "test" }, Array.Empty<string>(),
                true, true, 1, TimeSpan.Zero));
        }
    }
}
