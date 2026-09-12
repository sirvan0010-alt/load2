using MimeKit;

namespace MailLoadTester;

/// <summary>
/// Executes payload plugins in deterministic order. The pipeline is intentionally
/// transport-agnostic: SMTP admission, pacing, retries and authorization remain
/// exclusively under SmtpTestRunner/SmtpConnectionPool.
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
            await plugin.ApplyAsync(message, context).ConfigureAwait(false);
        }
    }
}
