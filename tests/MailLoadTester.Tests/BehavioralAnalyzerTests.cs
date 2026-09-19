using MailLoadTester;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class BehavioralAnalyzerTests
{
    [Fact]
    public void Analyze_DetectsBurstAndRecipientConcentration()
    {
        var start = DateTimeOffset.UnixEpoch;
        var events = Enumerable.Range(0, 25)
            .Select(i => new BehavioralMailEvent(start.AddMilliseconds(i * 100), "provider-a", "sender.example", "victim@example.test", SimulatedMailOutcome.Accepted))
            .ToArray();
        var result = new BehavioralAnalyzer().Analyze(events, new BehavioralAnalysisOptions(TimeSpan.FromSeconds(3), 20, 0.5, 0.6));
        Assert.Equal(25, result.EventCount);
        Assert.Equal(25, result.PeakEventsInBurst);
        Assert.True(result.BurstDetected);
        Assert.True(result.ConcentrationDetected);
        Assert.Contains("burst-velocity", result.Findings);
        Assert.Contains("recipient-concentration", result.Findings);
        Assert.True(result.Anomalous);
        Assert.InRange(result.AnomalyScore, 0, 1);
    }

    [Fact]
    public void Analyze_IsOrderIndependentAndDeterministic()
    {
        var start = DateTimeOffset.UnixEpoch;
        var events = Enumerable.Range(0, 8)
            .Select(i => new BehavioralMailEvent(start.AddSeconds(i), i % 2 == 0 ? "a" : "b", i % 2 == 0 ? "a.test" : "b.test", $"user{i}@example.test", SimulatedMailOutcome.Accepted))
            .ToArray();
        var analyzer = new BehavioralAnalyzer();
        var first = analyzer.Analyze(events);
        var reversed = analyzer.Analyze(events.Reverse());

        Assert.Equal(first with { Findings = null }, reversed with { Findings = null });
        Assert.Equal(first.Findings.OrderBy(x => x), reversed.Findings.OrderBy(x => x));
    }

    [Fact]
    public void Analyze_MapsProviderSimulatorEvents()
    {
        var start = DateTimeOffset.UnixEpoch;
        var simulated = new[]
        {
            new SimulatedMailEvent(start, "provider-a", "sender.example", "confirmation", "a@example.test", SimulatedMailOutcome.Accepted, 10, "pass"),
            new SimulatedMailEvent(start.AddSeconds(1), "provider-b", "sender.example", "confirmation", "b@example.test", SimulatedMailOutcome.Throttled, 20, "pass")
        };
        var result = new BehavioralAnalyzer().Analyze(simulated);
        Assert.Equal(2, result.EventCount);
        Assert.Equal(1.0, result.ProviderDiversity);
        Assert.Equal(0.5, result.RecipientConcentration);
    }

    [Fact]
    public void Analyze_HonorsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => new BehavioralAnalyzer().Analyze(
            new[] { new BehavioralMailEvent(DateTimeOffset.UtcNow, "p", "s.test", "r@test", SimulatedMailOutcome.Accepted) },
            cancellationToken: cts.Token));
    }

    [Fact]
    public void Analyze_RejectsInvalidEvent()
    {
        var item = new BehavioralMailEvent(DateTimeOffset.UtcNow, "", "sender.test", "recipient@test", SimulatedMailOutcome.Accepted);
        Assert.Throws<ArgumentException>(() => new BehavioralAnalyzer().Analyze(new[] { item }));
    }
}
