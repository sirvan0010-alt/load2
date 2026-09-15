namespace MailLoadTester.Tests;

public sealed class ProviderSimulatorTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalEvents()
    {
        var simulator = new DeterministicMailProviderSimulator("provider-a");
        var request = new ProviderSimulationRequest(
            "recipient@example.test",
            "sender.example.test",
            "confirmation",
            20,
            DateTimeOffset.UnixEpoch,
            Seed: 42);

        var first = simulator.Simulate(request);
        var second = simulator.Simulate(request);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Simulation_DoesNotRequireNetworkAndHonorsCancellation()
    {
        var simulator = new DeterministicMailProviderSimulator("provider-a");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ProviderSimulationRequest(
            "recipient@example.test",
            "sender.example.test",
            "welcome",
            10,
            DateTimeOffset.UnixEpoch);

        Assert.Throws<OperationCanceledException>(() => simulator.Simulate(request, cts.Token));
    }

    [Fact]
    public void Outcomes_RespectConfiguredThrottleRate()
    {
        var simulator = new DeterministicMailProviderSimulator(
            "provider-a",
            throttleRate: 1.0,
            temporaryFailureRate: 0.0);
        var request = new ProviderSimulationRequest(
            "recipient@example.test",
            "sender.example.test",
            "confirmation",
            10,
            DateTimeOffset.UnixEpoch,
            Seed: 7);

        var events = simulator.Simulate(request);

        Assert.All(events, e => Assert.Equal(SimulatedMailOutcome.Throttled, e.Outcome));
    }

    [Fact]
    public void ProviderScenarioSimulator_ComposesWithB3ScenarioDefinition()
    {
        var simulator = new ProviderScenarioSimulator(new IMailProviderSimulator[]
        {
            new DeterministicMailProviderSimulator("provider-a", throttleRate: 0),
            new DeterministicMailProviderSimulator("provider-b", throttleRate: 1)
        });
        var scenario = new LoadScenarioDefinition(
            LoadScenarioKind.ProviderDistribution,
            "provider-distribution",
            10,
            2,
            0,
            Seed: 100);

        var events = simulator.Simulate(
            scenario,
            "recipient@example.test",
            "sender.example.test",
            "confirmation",
            DateTimeOffset.UnixEpoch);

        Assert.Equal(10, events.Count);
        Assert.Contains(events, e => e.ProviderId == "provider-a");
        Assert.Contains(events, e => e.ProviderId == "provider-b");
    }

    [Fact]
    public void ProviderScenarioSimulator_RejectsNonProviderScenario()
    {
        var simulator = new ProviderScenarioSimulator(new[]
        {
            new DeterministicMailProviderSimulator("provider-a")
        });
        var scenario = new LoadScenarioDefinition(
            LoadScenarioKind.NormalDelivery,
            "normal",
            10,
            1,
            0);

        Assert.Throws<ArgumentException>(() => simulator.Simulate(
            scenario,
            "recipient@example.test",
            "sender.example.test",
            "delivery",
            DateTimeOffset.UnixEpoch));
    }
}
