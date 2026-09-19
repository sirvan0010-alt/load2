namespace MailLoadTester.Core;

public sealed record AiAgentModelRequest(
    string TaskId,
    string AgentRole,
    string Repository,
    string ImmutableCommit,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AcceptanceCriteria,
    int Iteration,
    DateTimeOffset Deadline);

public sealed record AiAgentModelResponse(
    string Summary,
    IReadOnlyList<string> ProposedChanges,
    IReadOnlyList<string> ProposedTests,
    IReadOnlyList<string> Evidence,
    bool RequiresIndependentVerification);

public interface IAiAgentModelAdapter
{
    Task<AiAgentModelResponse> GenerateAsync(
        AiAgentModelRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Provider-neutral bridge from an AI model to the autonomous engineering agent.
/// Model output is evidence/proposal only; it is never treated as authorization,
/// proof of correctness, or a direct tool-execution command.
/// </summary>
public sealed class ModelBackedAiAgent : IAiAgent
{
    private readonly IAiAgentModelAdapter _model;

    public ModelBackedAiAgent(IAiAgentModelAdapter model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public async Task<AiAgentExecutionResult> ExecuteAsync(
        AiAgentContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var response = await _model.GenerateAsync(
            new AiAgentModelRequest(
                context.Task.TaskId,
                context.Task.AgentRole,
                context.Task.Repository,
                context.Task.Commit,
                context.Task.AllowedScopes,
                context.Task.AcceptanceCriteria,
                context.Iteration,
                context.Deadline),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (response is null)
            throw new InvalidOperationException("AI model adapter returned no response.");

        var evidence = response.Evidence ?? Array.Empty<string>();
        var changes = response.ProposedChanges ?? Array.Empty<string>();
        var tests = response.ProposedTests ?? Array.Empty<string>();

        var findings = new[]
        {
            new AiAgentFinding(
                string.IsNullOrWhiteSpace(response.Summary)
                    ? "Model produced a proposal without a summary."
                    : response.Summary,
                evidence,
                AgentEvidenceLevel.SourceDocumented)
        };

        var handoff = string.Join(
            Environment.NewLine,
            new[]
            {
                "Model output is unverified evidence only.",
                $"Summary: {response.Summary}",
                $"Proposed changes: {string.Join(", ", changes)}",
                $"Proposed tests: {string.Join(", ", tests)}",
                $"Independent verification required: {response.RequiresIndependentVerification || true}"
            });

        return new AiAgentExecutionResult(
            AgentRunStatus.NeedsEvidence,
            findings,
            changes,
            new Dictionary<string, string>
            {
                ["model_summary"] = response.Summary ?? string.Empty,
                ["verification_required"] = "true"
            },
            tests,
            CiRequired: true,
            AuthorizationPassed: !context.Task.RealTargetRequired || context.Task.Authorized,
            CancellationPassed: true,
            HardLimitsPassed: true,
            SecretsPassed: true,
            Handoff: handoff);
    }
}
