using Xunit;

namespace MailLoadTester.Tests;

public sealed class SmtpAccountPoolHubTests
{
    static MailTestOptions BaseOpts() => new(
        From: "a@t.l",
        Recipients: new[] { "b@t.l" },
        SmtpHost: "127.0.0.1",
        Port: 1,
        Security: SmtpSecurity.None,
        UseAuthentication: false,
        Username: "",
        Password: "",
        MessageCount: 1,
        IntervalMs: 0,
        BatchMode: false,
        BatchSize: 1,
        BatchPauseSeconds: 0,
        MaxConcurrency: 2,
        Subject: "s",
        Body: "b",
        DisplayName: "n",
        RandomTestData: false,
        TestMode: true,
        AllowedDomains: "",
        HtmlBody: false,
        Attachments: Array.Empty<string>(),
        CustomHeaders: new Dictionary<string, string>(),
        IgnoreCertificateErrors: false,
        MaxRetries: 0,
        DryRun: true);

    static SmtpAccount Acc(string id, string host = "127.0.0.1", int port = 25) =>
        new(id, host, port, SmtpSecurity.None, false, "", "");

    [Fact]
    public async Task GetOrCreatePool_IsOnePerAccountId()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a", port: 25), Acc("b", port: 26) });
        await using var hub = new SmtpAccountPoolHub(BaseOpts(), reg);
        var p1 = hub.GetOrCreatePool(reg.Snapshot()[0]);
        var p1b = hub.GetOrCreatePool(reg.Snapshot()[0]);
        var p2 = hub.GetOrCreatePool(reg.Snapshot()[1]);
        Assert.Same(p1, p1b);
        Assert.NotSame(p1, p2);
        Assert.Equal(2, hub.PoolCount);
    }

    [Fact]
    public async Task RentAsync_Throws_WhenAllQuarantined()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("only") });
        var health = new TransportHealthRegistry(1, 1, TimeSpan.FromHours(1));
        await using var hub = new SmtpAccountPoolHub(BaseOpts(), reg, health);
        health.RecordFailure(reg.Snapshot()[0].HealthKey, "smtp_5xx");
        await Assert.ThrowsAsync<InvalidOperationException>(() => hub.RentAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReportSendSuccess_RecoversAccount()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a") });
        var health = new TransportHealthRegistry(1, 1, TimeSpan.FromHours(1));
        await using var hub = new SmtpAccountPoolHub(BaseOpts(), reg, health);
        var a = reg.Snapshot()[0];
        hub.ReportSendFailure(a, new TimeoutException());
        Assert.Null(reg.TrySelect(health));
        hub.ReportSendSuccess(a);
        Assert.NotNull(reg.TrySelect(health));
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a") });
        var hub = new SmtpAccountPoolHub(BaseOpts(), reg);
        _ = hub.GetOrCreatePool(reg.Snapshot()[0]);
        await hub.DisposeAsync();
        await hub.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => hub.GetOrCreatePool(reg.Snapshot()[0]));
    }

    [Fact]
    public async Task GetOrCreate_StartsEmpty()
    {
        var reg = new SmtpAccountRegistry(new[] { Acc("a"), Acc("b") });
        await using var hub = new SmtpAccountPoolHub(BaseOpts(), reg);
        Assert.Equal(0, hub.PoolCount);
        hub.GetOrCreatePool(reg.Snapshot()[0]);
        Assert.Equal(1, hub.PoolCount);
    }
}
