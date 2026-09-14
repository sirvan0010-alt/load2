using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class AiAgentTaskLoaderTests
{
    [Fact]
    public void LoadsAndNormalizesTaskWithoutMutatingSourceCollections()
    {
        const string json = """
        {
          "taskId": " AI-001 ",
          "agentRole": " IMPLEMENTATION_AGENT ",
          "repository": "sirvan0010-alt/load2",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "allowedScopes": [" src/** ", "src/**", "tests/**"],
          "acceptanceCriteria": [" compile ", "compile"],
          "realTargetRequired": false,
          "authorized": true,
          "timeBudgetSeconds": 30,
          "maxIterations": 2
        }
        """;

        var task = new AiAgentTaskLoader().Load(json);

        Assert.Equal("AI-001", task.TaskId);
        Assert.Equal("IMPLEMENTATION_AGENT", task.AgentRole);
        Assert.Equal(new[] { "src/**", "tests/**" }, task.AllowedScopes);
        Assert.Equal(new[] { "compile" }, task.AcceptanceCriteria);
        Assert.Equal(TimeSpan.FromSeconds(30), task.TimeBudget);
        Assert.Equal(2, task.MaxIterations);
    }

    [Fact]
    public void RejectsMissingRepository()
    {
        const string json = """
        {
          "taskId": "AI-002",
          "agentRole": "TEST_AGENT",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "allowedScopes": ["tests/**"],
          "acceptanceCriteria": ["tests pass"],
          "timeBudgetSeconds": 10,
          "maxIterations": 1
        }
        """;

        Assert.Throws<InvalidDataException>(() => new AiAgentTaskLoader().Load(json));
    }

    [Fact]
    public async Task AsyncLoadHonorsCancellation()
    {
        const string json = "{}";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AiAgentTaskLoader().LoadAsync(stream, cts.Token));
    }

    [Fact]
    public void RejectsEmptyScopesAndAcceptanceCriteria()
    {
        const string json = """
        {
          "taskId": "AI-003",
          "agentRole": "TEST_AGENT",
          "repository": "sirvan0010-alt/load2",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "allowedScopes": [],
          "acceptanceCriteria": ["tests pass"],
          "timeBudgetSeconds": 10,
          "maxIterations": 1
        }
        """;

        Assert.Throws<InvalidDataException>(() => new AiAgentTaskLoader().Load(json));
    }
}
