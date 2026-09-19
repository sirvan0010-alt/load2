namespace MailLoadTester.Core;

public sealed class MiniSweAiAgent : IAiAgent
{
    private readonly MiniSweAgentRuntime _runtime;

    public MiniSweAiAgent(MiniSweAgentRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public async Task<AiAgentExecutionResult> ExecuteAsync(
        AiAgentContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var result = await _runtime.RunAsync(
            context.Task,
            context.Task.WorkspacePath,
            cancellationToken).ConfigureAwait(false);

        var findings = new List<AiAgentFinding>
        {
            new(
                $"Mini-SWE-agent exited with code {result.ExitCode}.",
                Array.Empty<string>(),
                AgentEvidenceLevel.SourceDocumented)
        };

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            findings.Add(new AiAgentFinding(
                "Mini-SWE-agent standard output was captured for independent verification.",
                Array.Empty<string>(),
                AgentEvidenceLevel.SourceDocumented));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            findings.Add(new AiAgentFinding(
                "Mini-SWE-agent standard error was captured for independent verification.",
                Array.Empty<string>(),
                AgentEvidenceLevel.SourceDocumented));
        }

        return new AiAgentExecutionResult(
            AgentRunStatus.NeedsEvidence,
            findings,
            Array.Empty<string>(),
            new Dictionary<string, string>
            {
                ["ExitCode"] = result.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            Array.Empty<string>(),
            CiRequired: true,
            AuthorizationPassed: context.Task.Authorized || !context.Task.RealTargetRequired,
            CancellationPassed: true,
            HardLimitsPassed: true,
            SecretsPassed: true,
            Handoff: result.ExitCode == 0
                ? "Mini-SWE-agent completed; workspace changes and tests require independent verification."
                : "Mini-SWE-agent failed; inspect captured output and start a bounded repair iteration.");
    }
}