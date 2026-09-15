namespace MailLoadTester;

public enum SimulatedMailOutcome
{
    Accepted,
    Throttled,
    TemporaryFailure,
    PermanentFailure
}

public sealed record SimulatedMailEvent(
    DateTimeOffset Timestamp,
    string ProviderId,
    string SenderDomain,
    string Workflow,
    string Recipient,
    SimulatedMailOutcome Outcome,
    int LatencyMs,
    string AuthenticationResult);

public sealed record ProviderSimulationRequest(
    string Recipient,
    string SenderDomain,
    string Workflow,
    int EventCount,
    DateTimeOffset StartTime,
    int Seed = 0);

public interface IMailProviderSimulator
{
    string ProviderId { get; }

    IReadOnlyList<SimulatedMailEvent> Simulate(
        ProviderSimulationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Deterministic local provider simulator. It never opens a network connection.
/// </summary>
public sealed class DeterministicMailProviderSimulator : IMailProviderSimulator
{
    private readonly double _throttleRate;
    private readonly double _temporaryFailureRate;
    private readonly int _minLatencyMs;
    private readonly int _maxLatencyMs;

    public DeterministicMailProviderSimulator(
        string providerId,
        double throttleRate = 0.05,
        double temporaryFailureRate = 0.02,
        int minLatencyMs = 20,
        int maxLatencyMs = 250)
    {
        if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("Provider id is required.", nameof(providerId));
        if (throttleRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(throttleRate));
        if (temporaryFailureRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(temporaryFailureRate));
        if (minLatencyMs < 0 || maxLatencyMs < minLatencyMs) throw new ArgumentOutOfRangeException(nameof(maxLatencyMs));

        ProviderId = providerId;
        _throttleRate = throttleRate;
        _temporaryFailureRate = temporaryFailureRate;
        _minLatencyMs = minLatencyMs;
        _maxLatencyMs = maxLatencyMs;
    }

    public string ProviderId { get; }

    public IReadOnlyList<SimulatedMailEvent> Simulate(
        ProviderSimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EventCount is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(request.EventCount));
        if (string.IsNullOrWhiteSpace(request.Recipient))
            throw new ArgumentException("Recipient is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SenderDomain))
            throw new ArgumentException("Sender domain is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Workflow))
            throw new ArgumentException("Workflow is required.", nameof(request));

        var random = new Random(request.Seed);
        var events = new List<SimulatedMailEvent>(request.EventCount);

        for (var i = 0; i < request.EventCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sample = random.NextDouble();
            var outcome = sample < _throttleRate
                ? SimulatedMailOutcome.Throttled
                : sample < _throttleRate + _temporaryFailureRate
                    ? SimulatedMailOutcome.TemporaryFailure
                    : SimulatedMailOutcome.Accepted;

            events.Add(new SimulatedMailEvent(
                request.StartTime.AddMilliseconds(i),
                ProviderId,
                request.SenderDomain,
                request.Workflow,
                request.Recipient,
                outcome,
                random.Next(_minLatencyMs, _maxLatencyMs + 1),
                outcome == SimulatedMailOutcome.Accepted ? "pass" : "unknown"));
        }

        return events;
    }
}
