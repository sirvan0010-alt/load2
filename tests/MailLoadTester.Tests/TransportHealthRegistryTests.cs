using Xunit;

namespace MailLoadTester.Tests;

public sealed class TransportHealthRegistryTests
{
    [Fact]
    public void NewEndpoint_IsHealthyAndAvailable()
    {
        var h = new TransportHealthRegistry(degradedAfter: 2, quarantineAfter: 3, quarantineDuration: TimeSpan.FromMinutes(1));
        Assert.Equal(EndpointHealthState.Healthy, h.GetState("smtp.example:587"));
        Assert.True(h.IsAvailable("smtp.example:587"));
    }

    [Fact]
    public void ConsecutiveFailures_DegradeThenQuarantine()
    {
        var h = new TransportHealthRegistry(2, 3, TimeSpan.FromMinutes(5));
        h.RecordFailure("h1", "timeout");
        Assert.Equal(EndpointHealthState.Healthy, h.GetState("h1"));
        h.RecordFailure("h1", "timeout");
        Assert.Equal(EndpointHealthState.Degraded, h.GetState("h1"));
        h.RecordFailure("h1", "smtp_5xx");
        Assert.Equal(EndpointHealthState.Quarantined, h.GetState("h1"));
        Assert.False(h.IsAvailable("h1"));
        var snap = h.Snapshot("h1");
        Assert.Equal(3, snap.ConsecutiveFailures);
        Assert.Equal("smtp_5xx", snap.LastFailureCategory);
        Assert.NotNull(snap.QuarantinedUntil);
    }

    [Fact]
    public void Success_ClearsQuarantineAndConsecutive()
    {
        var h = new TransportHealthRegistry(1, 2, TimeSpan.FromHours(1));
        h.RecordFailure("x");
        h.RecordFailure("x");
        Assert.False(h.IsAvailable("x"));
        h.RecordSuccess("x");
        Assert.True(h.IsAvailable("x"));
        Assert.Equal(EndpointHealthState.Healthy, h.GetState("x"));
        Assert.Equal(0, h.Snapshot("x").ConsecutiveFailures);
        Assert.Equal(1, h.Snapshot("x").TotalSuccesses);
    }

    [Fact]
    public void Quarantine_ExpiresAfterDuration()
    {
        var h = new TransportHealthRegistry(1, 1, TimeSpan.FromMilliseconds(50));
        h.RecordFailure("y", "io");
        Assert.False(h.IsAvailable("y"));
        Thread.Sleep(80);
        Assert.True(h.IsAvailable("y"));
        Assert.NotEqual(EndpointHealthState.Quarantined, h.GetState("y"));
    }

    [Fact]
    public async Task ConcurrentUpdates_DoNotThrow()
    {
        var h = new TransportHealthRegistry(3, 10, TimeSpan.FromSeconds(30));
        var tasks = Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            for (var n = 0; n < 100; n++)
            {
                if ((i + n) % 5 == 0)
                    h.RecordSuccess("ep-" + (i % 4));
                else
                    h.RecordFailure("ep-" + (i % 4), "timeout");
                _ = h.IsAvailable("ep-" + (i % 4));
                _ = h.GetState("ep-" + (i % 4));
            }
        }));
        await Task.WhenAll(tasks);
        Assert.NotEmpty(h.SnapshotAll());
    }

    [Fact]
    public void ClassifyFailure_MapsKnownTypes()
    {
        Assert.Equal("timeout", TransportHealthRegistry.ClassifyFailure(new TimeoutException()));
        Assert.Equal("cancelled", TransportHealthRegistry.ClassifyFailure(new OperationCanceledException()));
        Assert.Equal("auth", TransportHealthRegistry.ClassifyFailure(new MailKit.Security.AuthenticationException("x")));
    }
}
