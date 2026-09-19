using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailLoadTester;

/// <summary>
/// Provider-neutral boundary for a model that returns a structured execution plan.
/// Implementations may call an external model, but the returned plan is never trusted
/// until it passes ExecutionPlan validation and AiSupervisor authorization.
/// </summary>
public interface IStructuredAiPlanProvider
{
    ValueTask<string> CreatePlanJsonAsync(
        AiTaskContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Converts provider JSON into the existing bounded ExecutionPlan contract.
/// This parser performs schema validation only; authorization remains the supervisor's job.
/// </summary>
public sealed class StructuredAiExecutionPlanner : IExecutionPlanner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IStructuredAiPlanProvider _provider;

    public StructuredAiExecutionPlanner(IStructuredAiPlanProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async ValueTask<ExecutionPlan> CreatePlanAsync(
        AiTaskContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var json = await _provider.CreatePlanJsonAsync(context, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("AI provider returned an empty execution plan.");

        AiExecutionPlanDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AiExecutionPlanDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("AI provider returned invalid execution-plan JSON.", ex);
        }

        if (dto?.Actions is null || dto.Actions.Count == 0)
            throw new InvalidOperationException("AI provider returned no execution actions.");

        var actions = dto.Actions.Select(ToAction).ToArray();
        var plan = new ExecutionPlan(actions);
        plan.Validate();
        return plan;
    }

    private static AiAction ToAction(AiActionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ActionId))
            throw new InvalidOperationException("AI action is missing actionId.");
        if (string.IsNullOrWhiteSpace(dto.AgentId))
            throw new InvalidOperationException($"AI action '{dto.ActionId}' is missing agentId.");
        if (dto.Targets is null || dto.Targets.Count == 0)
            throw new InvalidOperationException($"AI action '{dto.ActionId}' has no targets.");

        if (!Enum.TryParse<AiActionKind>(dto.Kind, ignoreCase: true, out var kind))
            throw new InvalidOperationException($"AI action '{dto.ActionId}' has unsupported kind '{dto.Kind}'.");

        return new AiAction(
            dto.ActionId,
            kind,
            dto.AgentId,
            dto.Targets,
            dto.MaxMessages,
            dto.MaxConcurrency,
            dto.MaxDurationSeconds);
    }

    private sealed record AiExecutionPlanDto(
        IReadOnlyList<AiActionDto>? Actions);

    private sealed record AiActionDto(
        string ActionId,
        string Kind,
        string AgentId,
        IReadOnlyList<string>? Targets,
        int MaxMessages,
        int MaxConcurrency,
        int MaxDurationSeconds);
}
