using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmtpAccountRegistryTests
{
    static SmtpAccount Acc(string id, string host = "smtp.example", int port = 587) => new(
        id, host, port, SmtpSecurity.StartTls, true, id + "@ex", "secret-" + id);

    [Fact]
    public void FromOptions_MapsPrimaryFields()
    {
        var o = new MailTestOptions(
            From: "a@t.l", Recipients: new[] { "b@t.l" }, SmtpHost: "h", Port: 25,
            Security: SmtpSecurity.None, UseAuthentication: true, Username: "u", Password: "p",
            MessageCount: 1, IntervalMs: 0, BatchMode: false, BatchSize: 1, BatchPauseSeconds: 0,
            MaxConcurrency: 1, Subject: "s", Body: "b", DisplayName: "n", RandomTestData: false,
            TestMode: true, AllowedDomains: "", HtmlBody: false, Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(), IgnoreCertificateErrors: false,
            MaxRetries: 0, DryRun: true);
        var a = SmtpAccount.FromOptions(o);
        Assert.Equal("primary", a.Id);
        Assert.Equal("h", a.SmtpHost);
        Assert.Equal("u", a.Username);
        Assert.Equal("p", a.Password);
        Assert.Equal("primary|h:25", a.HealthKey);
    }

    [Fact]
    public void TrySelect_PrefersHealthy_OverDegraded()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a"), Acc("b") });
        var health = new TransportHealthRegistry(degradedAfter: 1, quarantineAfter: 5);
        health.RecordFailure(reg.Snapshot()[1].HealthKey, "timeout");
        Assert.Equal(EndpointHealthState.Degraded, health.GetState(reg.Snapshot()[1].HealthKey));

        var picks = new HashSet<string>();
        for (var i = 0; i < 20; i++)
        {
            var s = reg.TrySelect(health);
            Assert.NotNull(s);
            picks.Add(s!.Id);
        }
        Assert.Contains("a", picks);
        Assert.DoesNotContain("b", picks);
    }

    [Fact]
    public void TrySelect_FallsBackToDegraded_WhenNoHealthy()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a"), Acc("b") });
        var health = new TransportHealthRegistry(1, 10);
        foreach (var a in reg.Snapshot())
            health.RecordFailure(a.HealthKey, "io");
        var s = reg.TrySelect(health);
        Assert.NotNull(s);
        Assert.Equal(EndpointHealthState.Degraded, health.GetState(s!.HealthKey));
    }

    [Fact]
    public void TrySelect_ReturnsNull_WhenAllQuarantined()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a") });
        var health = new TransportHealthRegistry(1, 1, TimeSpan.FromHours(1));
        health.RecordFailure(reg.Snapshot()[0].HealthKey, "smtp_5xx");
        Assert.Equal(EndpointHealthState.Quarantined, health.GetState(reg.Snapshot()[0].HealthKey));
        Assert.Null(reg.TrySelect(health));
    }

    [Fact]
    public void ReportSuccess_ClearsQuarantine()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a") });
        var health = new TransportHealthRegistry(1, 1, TimeSpan.FromHours(1));
        var a = reg.Snapshot()[0];
        reg.ReportFailure(health, a, new TimeoutException());
        Assert.Null(reg.TrySelect(health));
        reg.ReportSuccess(health, a);
        Assert.NotNull(reg.TrySelect(health));
    }

    [Fact]
    public async Task ConcurrentSelect_DoesNotThrow()
    {
        var reg = new SmtpAccountRegistry(Enumerable.Range(0, 4).Select(i => Acc("a" + i)).ToArray());
        var health = new TransportHealthRegistry();
        var tasks = Enumerable.Range(0, 40).Select(__ => Task.Run(() =>
        {
            for (var n = 0; n < 50; n++)
                reg.TrySelect(health);
        }));
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void ApplyTo_DoesNotLeakIntoRunReportJson()
    {
        var baseOpts = new MailTestOptions(
            From: "a@t.l", Recipients: new[] { "b@t.l" }, SmtpHost: "base", Port: 25,
            Security: SmtpSecurity.None, UseAuthentication: true, Username: "u", Password: "BASESECRET",
            MessageCount: 1, IntervalMs: 0, BatchMode: false, BatchSize: 1, BatchPauseSeconds: 0,
            MaxConcurrency: 1, Subject: "s", Body: "b", DisplayName: "n", RandomTestData: false,
            TestMode: true, AllowedDomains: "", HtmlBody: false, Attachments: Array.Empty<string>(),
            CustomHeaders: new Dictionary<string, string>(), IgnoreCertificateErrors: false,
            MaxRetries: 0, DryRun: true);
        var acc = Acc("x");
        var merged = acc.ApplyTo(baseOpts);
        Assert.Equal("secret-x", merged.Password);
        var report = RunReportBuilder.Create("r", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, merged,
            new MailTestResult(1, 0, 0, TimeSpan.Zero, "", 0, 0, 0, 0, 0, 0, 0));
        var json = RunReportBuilder.ToJson(report);
        Assert.DoesNotContain("secret-x", json, StringComparison.Ordinal);
        Assert.DoesNotContain("BASESECRET", json, StringComparison.Ordinal);
    }
}
