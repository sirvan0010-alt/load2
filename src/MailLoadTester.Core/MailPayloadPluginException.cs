namespace MailLoadTester;

/// <summary>
/// Thrown when an <see cref="IMailPayloadPlugin"/> fails during pipeline execution.
/// Surfaces as a normal per-message failure in <see cref="SmtpTestRunner"/> — there is
/// no separate plugin-retry path. SMTP-level retries may re-apply the full pipeline
/// for the same logical message, which is intentional and bounded by MaxRetries.
/// </summary>
public sealed class MailPayloadPluginException : Exception
{
    public string PluginName { get; }
    public int PluginOrder { get; }

    public MailPayloadPluginException(string pluginName, int pluginOrder, Exception inner)
        : base(
            $"Payload plugin '{pluginName}' (Order={pluginOrder}) failed: {inner.Message}",
            inner)
    {
        PluginName = pluginName ?? "";
        PluginOrder = pluginOrder;
    }
}
