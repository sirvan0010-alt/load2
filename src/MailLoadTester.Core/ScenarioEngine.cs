namespace MailLoadTester;

/// <summary>
/// Single execution adapter for typed scenarios. Scenario metadata is validated here,
/// then the existing SMTP runner remains the only transport/execution engine.
/// </summary>
public sealed class ScenarioEngine
{
    private readonly SmtpTestRunner _runner;

    public ScenarioEngine(SmtpTestRunner? runner = null)
    {
        _runner = runner ?? new SmtpTestRunner();
    }

    public MailTestOptions Prepare(LoadScenarioDefinition definition, MailTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);
        definition.Validate();
        return options;
    }

    public Task<MailTestResult> RunAsync(
        LoadScenarioDefinition definition,
        MailTestOptions options,
        IProgress<ProgressUpdate> progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(progress);
        SecurityExecutionGate.Validate(definition, ct);
        var prepared = Prepare(definition, options);
        return _runner.RunAsync(prepared, progress, ct);
    }
}
