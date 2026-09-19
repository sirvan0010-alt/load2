namespace MailLoadTester;

/// <summary>
/// The only bridge from an AI-authorized load-test action to the existing SMTP engine.
/// It deliberately accepts already-authorized actions and delegates transport entirely
/// to SmtpTestRunner.
/// </summary>
public sealed class AiLoadTestExecutor
{
    private readonly SmtpTestRunner _runner;

    public AiLoadTestExecutor(SmtpTestRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<MailTestResult> ExecuteAsync(
        AiAction action,
        AiTaskContext context,
        IProgress<ProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(progress);

        cancellationToken.ThrowIfCancellationRequested();

        if (action.Kind != AiActionKind.LoadTest)
            throw new InvalidOperationException(
                $"AI action '{action.ActionId}' is '{action.Kind}' and cannot be executed by the SMTP load-test executor.");

        if (action.MaxMessages > context.Options.MessageCount)
            throw new InvalidOperationException("AI action exceeds the configured message budget.");

        if (action.MaxConcurrency > context.Options.MaxConcurrency)
            throw new InvalidOperationException("AI action exceeds the configured concurrency budget.");

        var options = context.Options with
        {
            MessageCount = action.MaxMessages,
            MaxConcurrency = action.MaxConcurrency,
            DurationSeconds = action.MaxDurationSeconds > 0
                ? Math.Min(action.MaxDurationSeconds, context.Options.DurationSeconds > 0
                    ? context.Options.DurationSeconds
                    : action.MaxDurationSeconds)
                : context.Options.DurationSeconds
        };

        // Re-run the authoritative authorization gate immediately before network execution.
        // This keeps the executor safe even when called outside AiExecutionCoordinator.
        AuthorizationGate.EnsureSendAuthorized(options);

        return await _runner.RunAsync(options, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
