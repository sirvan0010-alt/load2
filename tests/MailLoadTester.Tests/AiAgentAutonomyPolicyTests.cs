using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class AiAgentAutonomyPolicyTests
{
    [Fact]
    public void RejectsUnauthorizedRealTarget()
    {
        var policy = new AiAgentAutonomyPolicy(5, TimeSpan.FromMinutes(1));
        var task = TaskFor(realTarget: true, authorized: false);

        var exception = Assert.Throws<InvalidOperationException>(() => policy.Validate(task));

        Assert.Contains("explicit authorization", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAutomaticMergeAndRelease()
    {
        var task = TaskFor(false, false);

        Assert.Throws<InvalidOperationException>(() =>
            new AiAgentAutonomyPolicy(5, TimeSpan.FromMinutes(1), AllowAutomaticMerge: true).Validate(task));

        Assert.Throws<InvalidOperationException>(() =>
            new AiAgentAutonomyPolicy(5, TimeSpan.FromMinutes(1), AllowAutomaticRelease: true).Validate(task));
    }

    [Fact]
    public void AcceptsAuthorizedNonTargetTask()
    {
        var policy = new AiAgentAutonomyPolicy(5, TimeSpan.FromMinutes(1));
        policy.Validate(TaskFor(false, false));
    }

    private static AiAgentTask TaskFor(bool realTarget, bool authorized) => new(
        "POLICY-001",
        "TEST_AGENT",
        "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/MailLoadTester.Core" },
        new[] { "tests pass" },
        realTarget,
        authorized,
        TimeSpan.FromSeconds(10),
        2,
        Path.GetTempPath());
}
