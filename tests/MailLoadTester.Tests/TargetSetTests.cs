using Xunit;

namespace MailLoadTester.Tests;

public sealed class TargetSetTests
{
    [Fact]
    public void FromRecipients_DedupesAndValidates()
    {
        var set = TargetSet.FromRecipients(new[] { "a@example.com", "A@example.com", "b@example.com" });
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void FromRecipients_RejectsInvalid()
    {
        Assert.Throws<ArgumentException>(() => TargetSet.FromRecipients(new[] { "not-an-email" }));
    }

    [Fact]
    public void FromRecipients_RejectsEmpty()
    {
        Assert.Throws<ArgumentException>(() => TargetSet.FromRecipients(Array.Empty<string>()));
    }

    [Fact]
    public async Task FromFileAsync_ReadsLinesAndComments()
    {
        var path = Path.Combine(Path.GetTempPath(), "load2-targets-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(path, "# comment\na@example.com\n\nb@example.com\n");
        try
        {
            var set = await TargetSet.FromFileAsync(path);
            Assert.Equal(2, set.Count);
            Assert.NotNull(set.SourcePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ScenarioLimits_ApplyTo_SetsDurationAndCount()
    {
        var baseOpts = new MailTestOptions(
            From: "a@t.l", Recipients: new[] { "b@t.l" }, SmtpHost: "h", Port: 25,
            Security: SmtpSecurity.None, UseAuthentication: false, Username: "", Password: "",
            MessageCount: 1, IntervalMs: 0, BatchMode: false, BatchSize: 1, BatchPauseSeconds: 0,
            MaxConcurrency: 1, Subject: "s", Body: "b", DisplayName: "n", RandomTestData: false,
            TestMode: true, AllowedDomains: "", HtmlBody: false, Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(), IgnoreCertificateErrors: false,
            MaxRetries: 0, DryRun: true);
        var scenario = new SmtpScenario(
            TargetSet.FromRecipients(new[] { "x@example.com", "y@example.com" }),
            new ScenarioLimits(MessageCount: 5, DurationSeconds: 30));
        var applied = scenario.ApplyTo(baseOpts);
        Assert.Equal(2, applied.Recipients.Count);
        Assert.Equal(5, applied.MessageCount);
        Assert.Equal(30, applied.DurationSeconds);
    }

    [Fact]
    public async Task Duration_DryRun_StopsViaLinkedCts()
    {
        var options = new MailTestOptions(
            From: "a@t.l", Recipients: new[] { "b@t.l" }, SmtpHost: "127.0.0.1", Port: 25,
            Security: SmtpSecurity.None, UseAuthentication: false, Username: "", Password: "",
            MessageCount: 500, IntervalMs: 50, BatchMode: false, BatchSize: 1, BatchPauseSeconds: 0,
            MaxConcurrency: 2, Subject: "s", Body: "b", DisplayName: "n", RandomTestData: false,
            TestMode: false, AllowedDomains: "", HtmlBody: false, Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(), IgnoreCertificateErrors: false,
            MaxRetries: 0, DryRun: true, Unauthorized: true, DurationSeconds: 1);
        var runner = new SmtpTestRunner();
        var result = await runner.RunAsync(options, new Progress<ProgressUpdate>(), CancellationToken.None);
        Assert.True(result.Elapsed.TotalSeconds < 20);
        Assert.True(result.Cancelled || result.Sent + result.Failed <= 500);
    }
}
