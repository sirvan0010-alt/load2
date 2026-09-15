using Xunit;

namespace MailLoadTester.Tests;

public sealed class SecurityExecutionGateTests
{
    static LoadScenarioDefinition Base(
        bool testMode = true,
        bool dryRun = false,
        bool unauthorized = false,
        int messageCount = 1,
        int maxConcurrency = 1,
        int intervalMs = 0,
        int durationSeconds = 0) => new(
            Kind: LoadScenarioKind.NormalDelivery,
            Name: "security-gate-test",
            MessageCount: messageCount,
            MaxConcurrency: maxConcurrency,
            IntervalMs: intervalMs,
            DurationSeconds: durationSeconds,
            TestMode: testMode,
            DryRun: dryRun,
            Unauthorized: unauthorized);

    [Fact]
    public void TestMode_PassesWithoutExplicitAuthorization()
    {
        SecurityExecutionGate.Validate(Base());
    }

    [Fact]
    public void DryRun_PassesWithoutExplicitAuthorization()
    {
        SecurityExecutionGate.Validate(Base(testMode: false, dryRun: true));
    }

    [Fact]
    public void LiveScenario_RequiresExplicitAuthorization()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecurityExecutionGate.Validate(Base(testMode: false)));
        Assert.Contains("authorization", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(10_001, 1, 0, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(1, 21, 0, 0)]
    [InlineData(1, 1, -1, 0)]
    [InlineData(1, 1, 0, 86_401)]
    public void HardLimits_AreEnforced(int messageCount, int maxConcurrency, int intervalMs, int durationSeconds)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            SecurityExecutionGate.Validate(Base(
                messageCount: messageCount,
                maxConcurrency: maxConcurrency,
                intervalMs: intervalMs,
                durationSeconds: durationSeconds)));
    }

    [Fact]
    public void Scope_RequiresName()
    {
        var scenario = Base() with { Name = " " };
        Assert.Throws<ArgumentException>(() => SecurityExecutionGate.Validate(scenario));
    }

    [Fact]
    public void Cancellation_IsCheckedBetweenSecurityStages()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            SecurityExecutionGate.Validate(Base(), cts.Token));
    }

    [Fact]
    public void AuthorizedLiveScenario_Passes()
    {
        SecurityExecutionGate.Validate(Base(testMode: false, unauthorized: true));
    }
}
