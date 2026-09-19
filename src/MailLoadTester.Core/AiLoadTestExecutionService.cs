namespace MailLoadTester;

/// <summary>
/// Wires the provider-neutral AI planner to the existing authorization and SMTP execution
/// pipeline. AI is opt-in: callers must explicitly construct this service.
/// </summary>
public sealed class AiLoadTestExecutionService
{
    private readonly AiExecutionCoordinator _coordinator;
    private readonly AiLoadTestExecutor _executor;

    public AiLoadTestExecutionService(
        AiExecutionCoordinator coordinator,
        AiLoadTestExecutor executor)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<IReadOnlyList<MailTestResult>> ExecuteAsync(
        AiTaskContext context,
        IProgress<ProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(progress);

        var actions = await _coordinator.PrepareAsync(context, cancellationToken)
            .ConfigureAwait(false);

        var results = new List<MailTestResult>(actions.Count);
        foreach (var action in actions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (action.Kind != AiActionKind.LoadTest)
                throw new InvalidOperationException(
                    $"Unsupported AI execution action: {action.Kind}");

            results.Add(await _executor.ExecuteAsync(
                action, context, progress, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }
}
