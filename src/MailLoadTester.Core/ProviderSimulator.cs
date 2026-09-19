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
    private readonly double _permanentFailureRate;
    private readonly int _minLatencyMs;
    private readonly int _maxLatencyMs;

    public DeterministicMailProviderSimulator(
        string providerId,
        double throttleRate = 0.05,
        double temporaryFailureRate = 0.02,
        int minLatencyMs = 20,
        int maxLatencyMs = 250,
        double permanentFailureRate = 0)
    {
        if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("Provider id is required.", nameof(providerId));
        if (throttleRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(throttleRate));
        if (temporaryFailureRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(temporaryFailureRate));
        if (permanentFailureRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(permanentFailureRate));
        if (throttleRate + temporaryFailureRate + permanentFailureRate > 1)
            throw new ArgumentException("Outcome probabilities must sum to <= 1.");
        if (minLatencyMs < 0 || maxLatencyMs < minLatencyMs) throw new ArgumentOutOfRangeException(nameof(maxLatencyMs));

        ProviderId = providerId;
        _throttleRate = throttleRate;
        _temporaryFailureRate = temporaryFailureRate;
        _permanentFailureRate = permanentFailureRate;
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
                    : sample < _throttleRate + _temporaryFailureRate + _permanentFailureRate
                        ? SimulatedMailOutcome.PermanentFailure
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

/// <summary>
/// Composes B4 with the existing typed B3 scenario definition without creating a
/// second SMTP queue, pacing system or retry policy. This path is simulation-only.
/// </summary>
public sealed class ProviderScenarioSimulator
{
    private readonly IReadOnlyList<IMailProviderSimulator> _providers;

    public ProviderScenarioSimulator(IEnumerable<IMailProviderSimulator> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _providers = providers.Where(static p => p is not null).ToArray();
        if (_providers.Count == 0)
            throw new ArgumentException("At least one provider simulator is required.", nameof(providers));

        var duplicate = _providers
            .GroupBy(static p => p.ProviderId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static g => g.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Duplicate provider id: {duplicate.Key}.", nameof(providers));
    }

    public IReadOnlyList<SimulatedMailEvent> Simulate(
        LoadScenarioDefinition scenario,
        string recipient,
        string senderDomain,
        string workflow,
        DateTimeOffset startTime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        scenario.Validate();
        if (scenario.Kind is not (LoadScenarioKind.ProviderDistribution
            or LoadScenarioKind.SubscriptionBombSimulation
            or LoadScenarioKind.DoubleOptInSimulation
            or LoadScenarioKind.AntiAbuseControlSimulation
            or LoadScenarioKind.FailureInjection
            or LoadScenarioKind.MailboxQuota))
            throw new ArgumentException("Provider simulation is only valid for provider/lab scenario kinds.", nameof(scenario));

        var events = new List<SimulatedMailEvent>(scenario.MessageCount);
        var baseSeed = scenario.Seed;
        var perProvider = Math.Max(1, (scenario.MessageCount + _providers.Count - 1) / _providers.Count);
        var remaining = scenario.MessageCount;
        var providerIndex = 0;

        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = _providers[providerIndex % _providers.Count];
            var count = Math.Min(perProvider, remaining);
            events.AddRange(provider.Simulate(
                new ProviderSimulationRequest(
                    recipient,
                    senderDomain,
                    workflow,
                    count,
                    startTime.AddMilliseconds(events.Count),
                    Seed: unchecked(baseSeed + providerIndex)),
                cancellationToken));
            remaining -= count;
            providerIndex++;
        }

        return events;
    }
}
