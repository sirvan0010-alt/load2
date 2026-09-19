namespace MailLoadTester;

/// <summary>
/// Bounded action kinds available to the AI orchestration layer.
/// The orchestration layer selects existing load2 operations; it does not own SMTP transport.
/// </summary>
public enum AiActionKind
{
    Diagnostics,
    LoadTest
}

public sealed record AiTaskContext(
    MailTestOptions Options,
    IReadOnlySet<string>? AllowedTargets = null);

public sealed record AiAction(
    string ActionId,
    AiActionKind Kind,
    string AgentId,
    IReadOnlyList<string> Targets,
    int MaxMessages,
    int MaxConcurrency,
    int MaxDurationSeconds);

public sealed record AiActionDecision(bool Allowed, string Reason)
{
    public static AiActionDecision Allow() => new(true, "allowed");
    public static AiActionDecision Deny(string reason) => new(false, reason);
}

public sealed record ExecutionPlan(IReadOnlyList<AiAction> Actions)
{
    public void Validate()
    {
        if (Actions.Count == 0)
            throw new ArgumentException("ExecutionPlan musí obsahovat alespoň jednu akci.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in Actions)
        {
            if (string.IsNullOrWhiteSpace(action.ActionId) || !ids.Add(action.ActionId))
                throw new ArgumentException("ExecutionPlan obsahuje duplicitní nebo prázdné ActionId.");
            if (string.IsNullOrWhiteSpace(action.AgentId))
                throw new ArgumentException($"Akce '{action.ActionId}' nemá AgentId.");
            if (action.Targets.Count == 0)
                throw new ArgumentException($"Akce '{action.ActionId}' nemá cíle.");
            if (action.MaxMessages is < 1 or > 10_000)
                throw new ArgumentException($"Akce '{action.ActionId}' má nepovolený MaxMessages.");
            if (action.MaxConcurrency is < 1 or > 20)
                throw new ArgumentException($"Akce '{action.ActionId}' má nepovolený MaxConcurrency.");
            if (action.MaxDurationSeconds is < 0 or > 86_400)
                throw new ArgumentException($"Akce '{action.ActionId}' má nepovolený MaxDurationSeconds.");
        }
    }
}

public interface IExecutionPlanner
{
    ValueTask<ExecutionPlan> CreatePlanAsync(
        AiTaskContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Deterministic planner used as the safe Phase-2 bridge before a model-backed
/// planner is introduced. It creates one bounded action from the already validated
/// MailTestOptions and never expands the configured target/message/concurrency scope.
/// </summary>
public sealed class ConfiguredExecutionPlanner : IExecutionPlanner
{
    private readonly string _agentId;

    public ConfiguredExecutionPlanner(string agentId = "LOAD_ENGINE_AGENT")
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("AgentId nesmí být prázdné.", nameof(agentId));
        _agentId = agentId;
    }

    public ValueTask<ExecutionPlan> CreatePlanAsync(
        AiTaskContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var targets = context.Options.DirectMxDelivery
            ? context.Options.Recipients
                .Select(r => r[(r.LastIndexOf('@') + 1)..].TrimEnd('.'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : new[] { context.Options.SmtpHost };

        var action = new AiAction(
            ActionId: Guid.NewGuid().ToString("N"),
            Kind: AiActionKind.LoadTest,
            AgentId: _agentId,
            Targets: targets,
            MaxMessages: context.Options.MessageCount,
            MaxConcurrency: context.Options.MaxConcurrency,
            MaxDurationSeconds: context.Options.DurationSeconds);

        return ValueTask.FromResult(new ExecutionPlan(new[] { action }));
    }
}

/// <summary>
/// Phase-2 orchestration boundary. Planning and authorization are separate from
/// actual execution so the existing SmtpTestRunner remains the sole SMTP executor.
/// </summary>
public sealed class AiExecutionCoordinator
{
    private readonly IExecutionPlanner _planner;
    private readonly IAiReplanner _replanner;
    private readonly AiSupervisor _supervisor;
    private readonly int _maxReplans;

    public AiExecutionCoordinator(
        IExecutionPlanner planner,
        AiSupervisor supervisor,
        IAiReplanner? replanner = null,
        int maxReplans = 2)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _replanner = replanner ?? new ConservativeAiReplanner();

        if (maxReplans is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(maxReplans));
        _maxReplans = maxReplans;
    }

    public async ValueTask<IReadOnlyList<AiAction>> PrepareAsync(
        AiTaskContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var plan = await _planner.CreatePlanAsync(context, cancellationToken)
            .ConfigureAwait(false);

        return await _supervisor.AuthorizePlanAsync(plan, context, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates and re-authorizes one bounded follow-up plan from a completed run.
    /// The method does not execute SMTP and never bypasses the supervisor guard.
    /// </summary>
    public async ValueTask<IReadOnlyList<AiAction>?> ReplanAsync(
        AiTaskContext context,
        IReadOnlyList<AiAction> previousActions,
        MailTestResult result,
        int replanOrdinal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(previousActions);
        ArgumentNullException.ThrowIfNull(result);

        if (replanOrdinal < 1 || replanOrdinal > _maxReplans)
            return null;

        var plan = await _replanner.CreateReplanAsync(
            new AiReplanContext(context, previousActions, result, replanOrdinal),
            cancellationToken).ConfigureAwait(false);

        if (plan is null)
            return null;

        return await _supervisor.AuthorizePlanAsync(
            plan, context, cancellationToken).ConfigureAwait(false);
    }
}


/// <summary>
/// Input for a bounded post-run replan. The replanner receives the existing result
/// and the previously authorized actions; it cannot widen the original scope.
/// </summary>
public sealed record AiReplanContext(
    AiTaskContext Task,
    IReadOnlyList<AiAction> PreviousActions,
    MailTestResult Result,
    int ReplanOrdinal);

public interface IAiReplanner
{
    ValueTask<ExecutionPlan?> CreateReplanAsync(
        AiReplanContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Conservative deterministic replan policy. It only reduces concurrency when the
/// previous run shows throttling, timeouts, SMTP 4xx/5xx failures, or a circuit break.
/// It never increases message count, concurrency, duration, or target scope.
/// A model-backed replanner can implement the same contract later.
/// </summary>
public sealed class ConservativeAiReplanner : IAiReplanner
{
    public ValueTask<ExecutionPlan?> CreateReplanAsync(
        AiReplanContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (context.ReplanOrdinal < 1)
            throw new ArgumentOutOfRangeException(nameof(context.ReplanOrdinal));

        if (context.PreviousActions.Count == 0)
            return ValueTask.FromResult<ExecutionPlan?>(null);

        var result = context.Result;
        var needsBackoff =
            result.Cancelled ||
            result.CircuitBreakerOpen ||
            result.Timeouts > 0 ||
            result.Smtp4xx > 0 ||
            result.Smtp5xx > 0 ||
            result.Failed > 0;

        if (!needsBackoff)
            return ValueTask.FromResult<ExecutionPlan?>(null);

        var actions = context.PreviousActions.Select(action =>
        {
            var reducedConcurrency = Math.Max(1, action.MaxConcurrency / 2);
            return action with
            {
                ActionId = $"{action.ActionId}-replan-{context.ReplanOrdinal}",
                MaxConcurrency = reducedConcurrency
            };
        }).ToArray();

        return ValueTask.FromResult<ExecutionPlan?>(
            new ExecutionPlan(actions));
    }
}

public interface IAiActionGuard
{
    ValueTask<AiActionDecision> ValidateAsync(
        AiAction action,
        AiTaskContext context,
        CancellationToken cancellationToken);
}

public interface IMailLoadAgent
{
    string Id { get; }
}

public interface IAgentRegistry
{
    IReadOnlyCollection<IMailLoadAgent> Agents { get; }
    IMailLoadAgent GetRequired(string id);
}

public sealed class MailLoadAgentRegistry : IAgentRegistry
{
    private readonly IReadOnlyDictionary<string, IMailLoadAgent> _agents;

    public MailLoadAgentRegistry(IEnumerable<IMailLoadAgent> agents)
    {
        ArgumentNullException.ThrowIfNull(agents);

        var map = new Dictionary<string, IMailLoadAgent>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents)
        {
            ArgumentNullException.ThrowIfNull(agent);
            if (string.IsNullOrWhiteSpace(agent.Id))
                throw new ArgumentException("Agent musí mít neprázdné Id.");
            if (!map.TryAdd(agent.Id, agent))
                throw new ArgumentException($"Agent '{agent.Id}' je registrován vícekrát.");
        }

        _agents = map;
    }

    public IReadOnlyCollection<IMailLoadAgent> Agents => _agents.Values.ToArray();

    public IMailLoadAgent GetRequired(string id)
    {
        if (!_agents.TryGetValue(id, out var agent))
            throw new KeyNotFoundException($"Agent '{id}' není registrován.");
        return agent;
    }
}

/// <summary>
/// Deterministic safety boundary before an AI-selected operation reaches the existing engine.
/// It intentionally delegates authorization semantics to AuthorizationGate.
/// </summary>
public sealed class AiActionGuard : IAiActionGuard
{
    public ValueTask<AiActionDecision> ValidateAsync(
        AiAction action,
        AiTaskContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (action.Targets.Count == 0)
            return ValueTask.FromResult(AiActionDecision.Deny("action has no targets"));

        if (action.MaxMessages > context.Options.MessageCount)
            return ValueTask.FromResult(AiActionDecision.Deny("action exceeds configured MessageCount"));

        if (action.MaxConcurrency > context.Options.MaxConcurrency)
            return ValueTask.FromResult(AiActionDecision.Deny("action exceeds configured MaxConcurrency"));

        if (context.Options.DurationSeconds > 0 &&
            action.MaxDurationSeconds > context.Options.DurationSeconds)
            return ValueTask.FromResult(AiActionDecision.Deny("action exceeds configured DurationSeconds"));

        if (context.AllowedTargets is { Count: > 0 } allowed)
        {
            foreach (var target in action.Targets)
            {
                if (!allowed.Contains(target))
                    return ValueTask.FromResult(AiActionDecision.Deny($"target is outside the allowed scope: {target}"));
            }
        }

        if (action.Kind == AiActionKind.LoadTest)
        {
            try
            {
                AuthorizationGate.EnsureSendAuthorized(context.Options);
            }
            catch (InvalidOperationException ex)
            {
                return ValueTask.FromResult(AiActionDecision.Deny(ex.Message));
            }
        }

        return ValueTask.FromResult(AiActionDecision.Allow());
    }
}

/// <summary>
/// Minimal supervisor contract. It validates a plan and returns the actions that are
/// allowed to enter the existing execution pipeline. It never sends mail itself.
/// </summary>
public sealed class AiSupervisor
{
    private readonly IAgentRegistry _agents;
    private readonly IAiActionGuard _guard;

    public AiSupervisor(IAgentRegistry agents, IAiActionGuard guard)
    {
        _agents = agents ?? throw new ArgumentNullException(nameof(agents));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
    }

    public async ValueTask<IReadOnlyList<AiAction>> AuthorizePlanAsync(
        ExecutionPlan plan,
        AiTaskContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        plan.Validate();
        var allowed = new List<AiAction>(plan.Actions.Count);

        foreach (var action in plan.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _agents.GetRequired(action.AgentId);

            var decision = await _guard.ValidateAsync(action, context, cancellationToken)
                .ConfigureAwait(false);

            if (!decision.Allowed)
                throw new InvalidOperationException(
                    $"AI action '{action.ActionId}' blocked: {decision.Reason}");

            allowed.Add(action);
        }

        return allowed;
    }
}
