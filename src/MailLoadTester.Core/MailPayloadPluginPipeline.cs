using MimeKit;

namespace MailLoadTester;

/// <summary>
/// Executes payload plugins in deterministic order. The pipeline is intentionally
/// transport-agnostic: SMTP admission, pacing, retries and authorization remain
/// exclusively under SmtpTestRunner/SmtpConnectionPool.
///
/// Lifecycle / error policy (I-5):
/// - Discovery runs once per test run; broken assemblies/types are skipped by the loader.
/// - Plugins execute in Order, then Name order.
/// - Cancellation is observed before each plugin and is never wrapped.
/// - A plugin exception fails the current message (fail-fast); later plugins are not run.
/// - There is no plugin-only retry loop; only the runner's existing SMTP MaxRetries may
///   re-enter BuildMessage → pipeline for the same message index.
/// </summary>
public sealed class MailPayloadPluginPipeline
{
    private readonly IReadOnlyList<IMailPayloadPlugin> _plugins;

    public MailPayloadPluginPipeline(IEnumerable<IMailPayloadPlugin>? plugins = null)
    {
        _plugins = (plugins ?? Array.Empty<IMailPayloadPlugin>())
            .Where(static p => p is not null)
            .OrderBy(static p => p.Order)
            .ThenBy(static p => p.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public int Count => _plugins.Count;

    public IReadOnlyList<string> PluginNames => _plugins.Select(static p => p.Name).ToArray();

    public async Task ApplyAsync(MimeMessage message, MailPayloadPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var plugin in _plugins)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            try
            {
                await plugin.ApplyAsync(message, context).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Preserve cancellation semantics for STOP / linked tokens.
                throw;
            }
            catch (Exception ex)
            {
                // Fail this message only. Do not continue to later plugins.
                // Do not invent a plugin-retry path — SmtpTestRunner owns retries.
                throw new MailPayloadPluginException(plugin.Name, plugin.Order, ex);
            }
        }
    }
}
