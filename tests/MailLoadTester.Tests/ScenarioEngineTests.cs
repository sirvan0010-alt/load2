namespace MailLoadTester.Tests;

public sealed class ScenarioEngineTests
{
    [Fact]
    public void Prepare_ValidatesTypedScenarioAndPreservesOptions()
    {
        var options = new MailTestOptions(
            From: "sender@example.test",
            Recipients: new[] { "recipient@example.test" },
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
            MaxConcurrency: 1,
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

        var definition = new LoadScenarioDefinition(
            LoadScenarioKind.BurstDelivery,
            "controlled-burst",
            10,
            1,
            100);

        var prepared = new ScenarioEngine().Prepare(definition, options);

        Assert.Same(options, prepared);
    }

    [Fact]
    public void Prepare_RejectsUnauthorizedRealScenario()
    {
        var options = new MailTestOptions(
            From: "sender@example.test",
            Recipients: new[] { "recipient@example.test" },
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
            MaxConcurrency: 1,
            Subject: "scenario-test",
            Body: "scenario-test",
            DisplayName: "load2",
            RandomTestData: false,
            TestMode: false,
            AllowedDomains: "example.test",
            HtmlBody: false,
            Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(),
            IgnoreCertificateErrors: false,
            MaxRetries: 0,
            DryRun: false);

        var definition = new LoadScenarioDefinition(
            LoadScenarioKind.SustainedLoad,
            "real-run",
            10,
            1,
            100,
            TestMode: false,
            DryRun: false,
            Unauthorized: false);

        Assert.Throws<InvalidOperationException>(() => new ScenarioEngine().Prepare(definition, options));
    }
}
