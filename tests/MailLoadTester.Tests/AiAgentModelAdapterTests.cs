using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AiAgentModelAdapterTests
{
    [Fact]
    public async Task ModelBackedAgentBuildsImmutableTaskContext()
    {
        var adapter = new RecordingAdapter();
        var agent = new ModelBackedAiAgent(adapter, new AllowAllToolExecutor());
        var task = CreateTask();

        var result = await agent.ExecuteAsync(
            new AiAgentContext(task, CancellationToken.None, DateTimeOffset.UtcNow.AddMinutes(1), 2),
            CancellationToken.None);

        Assert.Equal(AgentRunStatus.NeedsEvidence, result.Status);
        Assert.Equal(task.Commit, adapter.Request!.Commit);
        Assert.Contains("Allowed scopes:", adapter.Request.Prompt);
        Assert.Contains("Acceptance criteria:", adapter.Request.Prompt);
    }

    [Fact]
    public async Task ModelBackedAgentPropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        var adapter = new CancellingAdapter();
        var agent = new ModelBackedAiAgent(adapter, new AllowAllToolExecutor());
        var task = CreateTask();

        // Must cancel — otherwise CancellingAdapter waits on infinite Delay and hangs CI.
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agent.ExecuteAsync(
            new AiAgentContext(task, cts.Token, DateTimeOffset.UtcNow.AddMinutes(1), 1),
            cts.Token));
    }

    [Fact]
    public async Task ModelBackedAgentPropagatesCancellationFromAdapterDelay()
    {
        using var cts = new CancellationTokenSource();
        var adapter = new CancellingAdapter();
        var agent = new ModelBackedAiAgent(adapter, new AllowAllToolExecutor());
        var task = CreateTask();

        var execute = agent.ExecuteAsync(
            new AiAgentContext(task, cts.Token, DateTimeOffset.UtcNow.AddMinutes(1), 1),
            cts.Token);

        // Cancel while adapter is awaiting Delay — exercises cooperative cancellation path.
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execute);
    }

    private static AiAgentTask CreateTask() => new(
        "TEST-001",
        "TEST_AGENT",
        "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/", "tests/" },
        new[] { "result is independently verified" },
        false,
        false,
        TimeSpan.FromMinutes(1),
        1,
        Path.GetFullPath(Path.GetTempPath()));

    private sealed class RecordingAdapter : IAiAgentModelAdapter
    {
        public AiAgentModelRequest? Request { get; private set; }

        public Task<AiAgentModelResponse> GenerateAsync(AiAgentModelRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new AiAgentModelResponse("candidate output", Array.Empty<string>()));
        }
    }

    private sealed class CancellingAdapter : IAiAgentModelAdapter
    {
        public async Task<AiAgentModelResponse> GenerateAsync(AiAgentModelRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new AiAgentModelResponse("unreachable", Array.Empty<string>());
        }
    }

    private sealed class AllowAllToolExecutor : IAiAgentToolExecutor
    {
        public bool IsAllowed(string toolName) => false;

        public Task<string> ExecuteAsync(string toolName, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No tools are enabled in this test adapter.");
    }
}
