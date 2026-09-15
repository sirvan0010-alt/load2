using Xunit;

namespace MailLoadTester.Tests;

public sealed class ScenarioEngineTests
{
    static MailTestOptions BaseOptions() => new(
        From: "sender@example.test",
        Recipients: new[] { "initial@example.test" },
        SmtpHost: "127.0.0.1",
        Port: 25,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 1,
        IntervalMs: 100,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 0,
        MaxConcurrency: 2,
        Subject: "scenario-test",
        Body: "scenario-test",
        DisplayName: "load2",
        RandomTestData: false,
        TestMode: true,
        AllowedDomains: "example.test",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 0,
        DryRun: true);

    static LoadScenarioDefinition Definition(LoadScenarioKind kind, int duration = 0, ScenarioTuning? tuning = null)
        => LoadScenarioCatalog.Create(
            kind,
            TargetSet.FromRecipients(new[] { "one@example.test", "two@example.test" }),
            messageCount: 7,
            durationSeconds: duration,
            tuning: tuning);

    [Fact]
    public void BurstDelivery_UsesExistingBurstControls()
    {
        var options = Definition(LoadScenarioKind.BurstDelivery).ApplyTo(BaseOptions());

        Assert.Equal(7, options.MessageCount);
        Assert.Equal(2, options.Recipients.Count);
        Assert.True(options.EnableBurstMode);
        Assert.Equal(10, options.BurstSize);
        Assert.Equal(30, options.BurstPauseSeconds);
    }

    [Fact]
    public void SustainedLoad_AddsBoundedDefaultDuration()
    {
        var options = Definition(LoadScenarioKind.SustainedLoad).ApplyTo(BaseOptions());

        Assert.Equal(300, options.DurationSeconds);
    }

    [Fact]
    public void SustainedLoad_PreservesExplicitDuration()
    {
        var options = Definition(LoadScenarioKind.SustainedLoad, duration: 42).ApplyTo(BaseOptions());

        Assert.Equal(42, options.DurationSeconds);
    }

    [Fact]
    public void ConnectionSaturation_UsesExistingConcurrencyAndPrewarmControls()
    {
        var options = Definition(LoadScenarioKind.ConnectionSaturation).ApplyTo(BaseOptions());

        Assert.Equal(20, options.MaxConcurrency);
        Assert.True(options.PreWarmConnections);
    }

    [Fact]
    public void ExplicitTuningOverridesScenarioDefaults()
    {
        var options = Definition(
            LoadScenarioKind.BurstDelivery,
            tuning: new ScenarioTuning(
                MaxConcurrency: 4,
                IntervalMs: 250,
                BurstSize: 4,
                BurstPauseSeconds: 5,
                EnableJitter: false,
                PaceProfile: "Standard"))
            .ApplyTo(BaseOptions());

        Assert.Equal(4, options.MaxConcurrency);
        Assert.Equal(250, options.IntervalMs);
        Assert.Equal(4, options.BurstSize);
        Assert.Equal(5, options.BurstPauseSeconds);
        Assert.False(options.EnableJitter);
        Assert.Equal("Standard", options.PaceProfile);
    }

    [Fact]
    public void SimulationOnlyScenario_CannotFallThroughToSmtpRunner()
    {
        var definition = Definition(LoadScenarioKind.SubscriptionBombSimulation);

        Assert.True(definition.RequiresSimulation);
        Assert.Throws<NotSupportedException>(() => definition.ApplyTo(BaseOptions()));
    }

    [Fact]
    public void ProviderDistribution_RequiresMultipleAccounts()
    {
        var definition = Definition(LoadScenarioKind.ProviderDistribution);

        Assert.True(definition.RequiresMultipleAccounts);
        Assert.Throws<ArgumentException>(() => definition.ApplyTo(BaseOptions()));
    }

    [Fact]
    public void ScenarioEngine_Prepare_UsesScenarioDefinition()
    {
        var engine = new ScenarioEngine();
        var options = engine.Prepare(Definition(LoadScenarioKind.BurstDelivery), BaseOptions());

        Assert.Equal(7, options.MessageCount);
        Assert.Equal(2, options.Recipients.Count);
        Assert.True(options.EnableBurstMode);
    }
}
