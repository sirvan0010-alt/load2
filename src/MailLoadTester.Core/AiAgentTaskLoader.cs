using System.Text.Json;

namespace MailLoadTester.Core;

/// <summary>
/// Loads a persisted agent task and freezes the repository context before execution.
/// The loader performs no network access and never resolves a moving branch reference.
/// </summary>
public sealed class AiAgentTaskLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    public async Task<AiAgentTask> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
            throw new ArgumentException("Task source must be readable.", nameof(source));

        var dto = await JsonSerializer.DeserializeAsync<AiAgentTaskDocument>(
            source, JsonOptions, cancellationToken).ConfigureAwait(false);

        if (dto is null)
            throw new InvalidDataException("Agent task document is empty.");

        return CreateTask(dto);
    }

    public AiAgentTask Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<AiAgentTaskDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("Agent task document is empty.");

        return CreateTask(dto);
    }

    private static AiAgentTask CreateTask(AiAgentTaskDocument dto)
    {
        var allowedScopes = ToReadOnlyList(dto.AllowedScopes, nameof(dto.AllowedScopes));
        var acceptanceCriteria = ToReadOnlyList(dto.AcceptanceCriteria, nameof(dto.AcceptanceCriteria));

        return new AiAgentTask(
            Required(dto.TaskId, nameof(dto.TaskId)),
            Required(dto.AgentRole, nameof(dto.AgentRole)),
            Required(dto.Repository, nameof(dto.Repository)),
            Required(dto.Commit, nameof(dto.Commit)),
            allowedScopes,
            acceptanceCriteria,
            dto.RealTargetRequired,
            dto.Authorized,
            TimeSpan.FromSeconds(dto.TimeBudgetSeconds),
            dto.MaxIterations);
    }

    private static string Required(string? value, string name)
        => !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidDataException($"{name} is required.");

    private static IReadOnlyList<string> ToReadOnlyList(
        IEnumerable<string>? values,
        string name)
    {
        if (values is null)
            throw new InvalidDataException($"{name} is required.");

        var result = values
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (result.Length == 0)
            throw new InvalidDataException($"{name} must contain at least one value.");

        return Array.AsReadOnly(result);
    }

    private sealed record AiAgentTaskDocument(
        string? TaskId,
        string? AgentRole,
        string? Repository,
        string? Commit,
        string[]? AllowedScopes,
        string[]? AcceptanceCriteria,
        bool RealTargetRequired,
        bool Authorized,
        int TimeBudgetSeconds,
        int MaxIterations);
}
