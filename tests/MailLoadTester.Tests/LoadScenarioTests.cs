namespace MailLoadTester.Tests;

public sealed class LoadScenarioTests
{
    [Fact]
    public void TestScenario_IsValidWithoutAuthorization()
    {
        var scenario = new LoadScenarioDefinition(
            LoadScenarioKind.SubscriptionBombSimulation,
            "controlled-subscription-test",
            100,
            4,
            10);

        scenario.Validate();
    }

    [Fact]
    public void NonTestScenario_RequiresAuthorization()
    {
        var scenario = new LoadScenarioDefinition(
            LoadScenarioKind.SustainedLoad,
            "authorized-run",
            10,
            1,
            0,
            TestMode: false,
            DryRun: false,
            Unauthorized: false);

        Assert.Throws<InvalidOperationException>(() => scenario.Validate());
    }
}
