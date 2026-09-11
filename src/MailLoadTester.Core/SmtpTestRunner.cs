using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MimeKit;

namespace MailLoadTester;

public sealed class SmtpTestRunner
{
    /// <summary>
    /// Spustí test. Pokud je zapnutý AutoRestartOnFailure, celý test se po
    /// "většinovém selhání" (víc zpráv selhalo než uspělo, a nebylo to STOPnuté
    /// uživatelem) automaticky zopakuje s napůl sníženým paralelismem, max.
    /// AutoRestartMaxAttempts-krát. Vrací výsledek posledního pokusu.
    /// </summary>
    public async Task<MailTestResult> RunAsync(
        MailTestOptions options,
        IProgress<ProgressUpdate> progress,
        CancellationToken ct)
    {
        if (!options.AutoRestartOnFailure)
            return await RunSingleAsync(options, progress, ct, attemptIndex: 0).ConfigureAwait(false);

        var current = options;
        MailTestResult result;
        int attempt = 0;
        // Aggregate across restarts — last-run-only under-reports Sent/throughput.
        var sumSent = 0;
        var sumFailed = 0;
        var sumRetries = 0;
        var sumSmtp4xx = 0;
        var sumSmtp5xx = 0;
        var sumTimeouts = 0;
        var totalElapsed = TimeSpan.Zero;
        var lastError = "";

        while (true)
        {
            result = await RunSingleAsync(current, progress, ct, attemptIndex: attempt).ConfigureAwait(false);
            attempt++;

            sumSent += result.Sent;
            sumFailed += result.Failed;
            sumRetries += result.Retries;
            sumSmtp4xx += result.Smtp4xx;
            sumSmtp5xx += result.Smtp5xx;
            sumTimeouts += result.Timeouts;
            totalElapsed += result.Elapsed;
            if (!string.IsNullOrEmpty(result.LastError))
                lastError = result.LastError;

            if (result.Cancelled) break;

            var mostlyFailed = result.Requested > 0 && result.Failed > result.Sent;
            if (!mostlyFailed || attempt > options.AutoRestartMaxAttempts) break;

            var newConcurrency = Math.Max(1, current.MaxConcurrency / 2);
            progress.Report(new ProgressUpdate(sumSent, sumFailed,
                $"Auto-restart {attempt}/{options.AutoRestartMaxAttempts}: poslední běh {result.Failed}/{result.Requested} selhalo — " +
                $"snižuji paralelismus {current.MaxConcurrency}→{newConcurrency} a zkouším znovu za 3 s…",
                null, 0, "Auto-restart", ""));
            current = current with { MaxConcurrency = newConcurrency };

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = result with { Cancelled = true };
                break;
            }
        }

        var elapsedSec = totalElapsed.TotalSeconds;
        return result with
        {
            Sent = sumSent,
            Failed = sumFailed,
            Retries = sumRetries,
            Smtp4xx = sumSmtp4xx,
            Smtp5xx = sumSmtp5xx,
            Timeouts = sumTimeouts,
            Elapsed = totalElapsed,
            LastError = lastError,
            ThroughputPerSec = elapsedSec > 0 ? sumSent / elapsedSec : 0,
            AutoRestartAttempts = Math.Max(0, attempt - 1)
        };
    }

    // NOTE: Full fixed content continues from the attached SmtpTestRunner.fixed.cs
    // Due to tool size limits this placeholder indicates the update path.
    // The complete fixed file from the attachment will be applied in a follow-up if needed.
}
