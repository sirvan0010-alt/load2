using Xunit;

namespace MailLoadTester.Tests;

public sealed class CrossRepositoryAiOrchestrationTests
{
    [Fact]
    public void Registry_Rejects_Duplicate_Repositories()
    {
        var descriptor = Descriptor("repo-a");
        Assert.Throws<ArgumentException>(() =>
            new MailLoadTester.Core.AiRepositoryRegistry(new[] { descriptor, descriptor }));
    }

    [Fact]
    public void Registry_Rejects_Path_Traversal_Scope()
    {
        Assert.Throws<ArgumentException>(() => Descriptor("repo-a", "../outside"));
    }

    [Fact]
    public void Supervisor_Rejects_Unknown_Capability()
    {
        var registry = new MailLoadTester.Core.AiRepositoryRegistry(new[] { Descriptor("repo-a") });
        var supervisor = new MailLoadTester.Core.CrossRepositoryAiSupervisor(registry);

        Assert.Throws<InvalidOperationException>(() => supervisor.Authorize(
            new MailLoadTester.Core.AiRepositoryTask(
                "task-1", "repo-a", "inspect repository", new[] { "Execute" })));
    }

    [Fact]
    public void Supervisor_Rejects_Network_Requirement()
    {
        var registry = new MailLoadTester.Core.AiRepositoryRegistry(new[] { Descriptor("repo-a") });
        var supervisor = new MailLoadTester.Core.CrossRepositoryAiSupervisor(registry);

        Assert.Throws<InvalidOperationException>(() => supervisor.Authorize(
            new MailLoadTester.Core.AiRepositoryTask(
                "task-1", "repo-a", "inspect repository", new[] { "Inspect" }, true)));
    }

    [Fact]
    public void Supervisor_Returns_Bounded_Actions()
    {
        var registry = new MailLoadTester.Core.AiRepositoryRegistry(new[] { Descriptor("repo-a") });
        var supervisor = new MailLoadTester.Core.CrossRepositoryAiSupervisor(registry);

        var actions = supervisor.Authorize(
            new MailLoadTester.Core.AiRepositoryTask(
                "task-1", "repo-a", "inspect and test", new[] { "Inspect", "Test" }));

        Assert.Equal(2, actions.Count);
        Assert.All(actions, action => Assert.Equal("repo-a", action.Repository));
        Assert.All(actions, action => Assert.Equal("src", action.Scope));
    }

    private static MailLoadTester.Core.AiRepositoryDescriptor Descriptor(
        string repository,
        string scope = "src")
        => new(
            repository,
            "main",
            new string('a', 40),
            new HashSet<MailLoadTester.Core.AiRepositoryCapability>
            {
                MailLoadTester.Core.AiRepositoryCapability.Inspect,
                MailLoadTester.Core.AiRepositoryCapability.Test,
                MailLoadTester.Core.AiRepositoryCapability.Verify
            },
            new HashSet<string>(StringComparer.Ordinal) { scope });
}
