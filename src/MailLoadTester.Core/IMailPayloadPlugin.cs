using MimeKit;

namespace MailLoadTester;

/// <summary>
/// Optional message-payload extension point executed after MIME construction and
/// before serialization/SMTP SEND. Plugins must not perform network I/O or bypass
/// the runner's rate, concurrency, cancellation, or authorization gates.
/// </summary>
public interface IMailPayloadPlugin
{
    /// <summary>Deterministic execution order. Lower values execute first.</summary>
    int Order { get; }

    /// <summary>Stable human-readable plugin identifier.</summary>
    string Name { get; }

    /// <summary>
    /// Mutates the already constructed message. The supplied cancellation token
    /// must be observed by asynchronous plugin work.
    /// </summary>
    Task ApplyAsync(MimeMessage message, MailPayloadPluginContext context);
}
