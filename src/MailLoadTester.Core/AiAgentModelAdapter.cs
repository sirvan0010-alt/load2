namespace MailLoadTester.Core;

public sealed record AiAgentModelRequest(
    string TaskId,
    string AgentRole,
    string Repository,
    string Commit,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AcceptanceCriteria,
    string Prompt,
    CancellationToken CancellationToken);

public sealed record AiAgentModelResponse(string Output, IReadOnlyList<string> ProposedActions);

public interface IAiAgentModelAdapter
{
    Task<AiAgentModelResponse> GenerateAsync(AiAgentModelRequest request, CancellationToken cancellationToken);
}

public sealed class ModelBackedAiAgent : IAiAgent
{
    private readonly IAiAgentModelAdapter _adapter;
    private readonly IAiAgentToolExecutor _tools;

    public ModelBackedAiAgent(IAiAgentModelAdapter adapter, IAiAgentToolExecutor tools)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
    }

    public async Task<AiAgentExecutionResult> ExecuteAsync(AiAgentContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var request = new AiAgentModelRequest(
            context.Task.TaskId,
            context.Task.AgentRole,
            context.Task.Repository,
            context.Task.Commit,
            context.Task.AllowedScopes,
            context.Task.AcceptanceCriteria,
            BuildPrompt(context),
            cancellationToken);

        var response = await _adapter.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var finding = new AiAgentFinding(response.Output, Array.Empty<string>(), AgentEvidenceLevel.SourceDocumented);
        return new AiAgentExecutionResult(
            AgentRunStatus.NeedsEvidence,
            new[] { finding },
            Array.Empty<string>(),
            new Dictionary<string, string>(),
            Array.Empty<string>(),
            CiRequired: true,
            AuthorizationPassed: context.Task.Authorized || !context.Task.RealTargetRequired,
            CancellationPassed: true,
            HardLimitsPassed: true,
            SecretsPassed: true,
            Handoff: "Model output requires independent verification before acceptance.");
    }

    private static string BuildPrompt(AiAgentContext context) =>
        $"Task: {context.Task.TaskId}\n" +
        $"Role: {context.Task.AgentRole}\n" +
        $"Repository: {context.Task.Repository}\n" +
        $"Immutable commit: {context.Task.Commit}\n" +
        $"Iteration: {context.Iteration}\n" +
        $"Allowed scopes:\n- {string.Join("\n- ", context.Task.AllowedScopes)}\n" +
        $"Acceptance criteria:\n- {string.Join("\n- ", context.Task.AcceptanceCriteria)}\n" +
        "Do not claim verification that has not been independently established. Do not access real targets unless the task explicitly authorizes it.";
}
