using System.Text.Json;

namespace MailLoadTester.Core;

public sealed record AiAgentEvidenceRecord(
    string TaskId,
    string ImmutableCommit,
    int Iteration,
    AgentPhase Phase,
    DateTimeOffset Timestamp,
    bool Succeeded,
    string Summary,
    IReadOnlyList<string> Findings);

public interface IAiAgentEvidenceStore
{
    Task AppendAsync(AiAgentEvidenceRecord record, CancellationToken cancellationToken = default);
    IReadOnlyList<AiAgentEvidenceRecord> Snapshot();
}

public sealed class InMemoryAiAgentEvidenceStore : IAiAgentEvidenceStore
{
    private readonly object _sync = new();
    private readonly List<AiAgentEvidenceRecord> _records = new();

    public Task AppendAsync(AiAgentEvidenceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            _records.Add(record);
        return Task.CompletedTask;
    }

    public IReadOnlyList<AiAgentEvidenceRecord> Snapshot()
    {
        lock (_sync)
            return _records.ToArray();
    }

    public string ToJson()
    {
        lock (_sync)
            return JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
    }
}
