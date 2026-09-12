#!/usr/bin/env python3
from pathlib import Path

# --- Models.cs ---
p = Path('src/MailLoadTester.Core/Models.cs')
t = p.read_text()
if 'AvgPrepWaitMs' in t:
    print('Models already patched')
else:
    old = '''    int AutoRestartAttempts = 0,
    string? SessionLogFile = null);'''
    new = '''    int AutoRestartAttempts = 0,
    string? SessionLogFile = null,
    /// <summary>Average wait in WaitBeforeSendAsync (absolute/recipient gates) for successful messages.</summary>
    double AvgPrepWaitMs = 0,
    /// <summary>Average wait for rate limiter + adaptive concurrency acquire.</summary>
    double AvgAdaptiveWaitMs = 0,
    /// <summary>Average wait inside SMTP pool RentAsync (connect/handshake when needed).</summary>
    double AvgPoolWaitMs = 0,
    /// <summary>Average wait inside AcquireSendSlotAsync (global SEND spacing gate).</summary>
    double AvgPaceWaitMs = 0,
    /// <summary>Average duration of the actual SmtpClient.SendAsync call.</summary>
    double AvgSmtpSendMs = 0);'''
    if old not in t:
        raise SystemExit('Models: target not found')
    p.write_text(t.replace(old, new, 1))
    print('Models patched')

# --- SmtpTestRunner.cs ---
p = Path('src/MailLoadTester.Core/SmtpTestRunner.cs')
t = p.read_text()
if 'AvgSmtpSendMs:' in t and 'samplePaceMs' in t and 'static double TicksToMs' in t:
    print('Runner already fully patched')
    raise SystemExit(0)

if 'var prepWaits = new ConcurrentBag<double>();' not in t:
    old = '        var latencies = new ConcurrentBag<double>();'
    new = '''        var latencies = new ConcurrentBag<double>();
        // FEAT-022: phase timing samples (successful logical messages only).
        var prepWaits = new ConcurrentBag<double>();
        var adaptiveWaits = new ConcurrentBag<double>();
        var poolWaits = new ConcurrentBag<double>();
        var paceWaits = new ConcurrentBag<double>();
        var smtpSends = new ConcurrentBag<double>();'''
    if old not in t:
        raise SystemExit('latencies bag not found')
    t = t.replace(old, new, 1)
    print('bags added')

if 'samplePrepMs' not in t:
    old = '''                    // Absolute pacing protections and recipient reservation are established
                    // once before the retry loop. Actual global SEND spacing is acquired
                    // immediately before every real SMTP SendAsync.
                    await pace.WaitBeforeSendAsync(
                        options.Recipients[(i - 1) % options.Recipients.Count], workerCt).ConfigureAwait(false);

                    // WaitBeforeSendAsync už rezervoval per-recipient slot (pokud je limit zapnutý).
                    // RateLimiter/AdaptiveConcurrency mohou být zrušeny ještě před vstupem do
                    // hlavního try/finally níže; v tom případě musíme rezervaci příjemce vrátit,
                    // jinak by při STOP/cancel zůstala jako phantom reservation až do konce okna.
                    var recipient = options.Recipients[(i - 1) % options.Recipients.Count];
                    var recipientCommitted = false;
                    var adaptiveAcquired = false;
                    var ledgerAccepted = false;

                    try
                    {
                        await rateLimiter.WaitAsync(workerCt).ConfigureAwait(false);
                        if (adaptive != null)
                        {
                            await adaptive.AcquireAsync(workerCt).ConfigureAwait(false);
                            adaptiveAcquired = true;
                        }
                    }'''
    new = '''                    // Absolute pacing protections and recipient reservation are established
                    // once before the retry loop. Actual global SEND spacing is acquired
                    // immediately before every real SMTP SendAsync.
                    var phaseT0 = Stopwatch.GetTimestamp();
                    await pace.WaitBeforeSendAsync(
                        options.Recipients[(i - 1) % options.Recipients.Count], workerCt).ConfigureAwait(false);
                    var phaseT1 = Stopwatch.GetTimestamp();

                    // WaitBeforeSendAsync už rezervoval per-recipient slot (pokud je limit zapnutý).
                    // RateLimiter/AdaptiveConcurrency mohou být zrušeny ještě před vstupem do
                    // hlavního try/finally níže; v tom případě musíme rezervaci příjemce vrátit,
                    // jinak by při STOP/cancel zůstala jako phantom reservation až do konce okna.
                    var recipient = options.Recipients[(i - 1) % options.Recipients.Count];
                    var recipientCommitted = false;
                    var adaptiveAcquired = false;
                    var ledgerAccepted = false;
                    // FEAT-022 phase samples for this message (ms); committed only on success.
                    double samplePrepMs = TicksToMs(phaseT1 - phaseT0);
                    double sampleAdaptiveMs = 0;
                    double samplePoolMs = 0;
                    double samplePaceMs = 0;
                    double sampleSmtpMs = 0;

                    try
                    {
                        var ad0 = Stopwatch.GetTimestamp();
                        await rateLimiter.WaitAsync(workerCt).ConfigureAwait(false);
                        if (adaptive != null)
                        {
                            await adaptive.AcquireAsync(workerCt).ConfigureAwait(false);
                            adaptiveAcquired = true;
                        }
                        sampleAdaptiveMs = TicksToMs(Stopwatch.GetTimestamp() - ad0);
                    }'''
    if old not in t:
        raise SystemExit('WaitBeforeSend block not found')
    t = t.replace(old, new, 1)
    print('prep/adaptive instrumented')

if 'samplePoolMs = TicksToMs' not in t:
    old = '''                                    client = await pool!.RentAsync(workerCt).ConfigureAwait(false);
                                    ReportPathEvents(Volatile.Read(ref sent), Volatile.Read(ref failed), i, workerId, client, pool!);'''
    new = '''                                    var poolT0 = Stopwatch.GetTimestamp();
                                    client = await pool!.RentAsync(workerCt).ConfigureAwait(false);
                                    samplePoolMs = TicksToMs(Stopwatch.GetTimestamp() - poolT0);
                                    ReportPathEvents(Volatile.Read(ref sent), Volatile.Read(ref failed), i, workerId, client, pool!);'''
    if old not in t:
        raise SystemExit('RentAsync not found')
    t = t.replace(old, new, 1)
    print('pool instrumented')

if 'samplePaceMs = TicksToMs' not in t:
    old = '''                                    await using (var sendLease =
                                        await pace.AcquireSendSlotAsync(workerCt).ConfigureAwait(false))
                                    {
                                        if (utf8Format != null)
                                            await client.SendAsync(utf8Format, message, workerCt).ConfigureAwait(false);
                                        else
                                            await client.SendAsync(message, workerCt).ConfigureAwait(false);
                                    }'''
    new = '''                                    var paceT0 = Stopwatch.GetTimestamp();
                                    await using (var sendLease =
                                        await pace.AcquireSendSlotAsync(workerCt).ConfigureAwait(false))
                                    {
                                        samplePaceMs = TicksToMs(Stopwatch.GetTimestamp() - paceT0);
                                        var smtpT0 = Stopwatch.GetTimestamp();
                                        if (utf8Format != null)
                                            await client.SendAsync(utf8Format, message, workerCt).ConfigureAwait(false);
                                        else
                                            await client.SendAsync(message, workerCt).ConfigureAwait(false);
                                        sampleSmtpMs = TicksToMs(Stopwatch.GetTimestamp() - smtpT0);
                                    }'''
    if old not in t:
        raise SystemExit('Send gate not found')
    t = t.replace(old, new, 1)
    print('pace/smtp instrumented')

if 'prepWaits.Add(samplePrepMs)' not in t:
    old = '                                latencies.Add(msgSw.Elapsed.TotalMilliseconds);'
    new = '''                                latencies.Add(msgSw.Elapsed.TotalMilliseconds);
                                prepWaits.Add(samplePrepMs);
                                adaptiveWaits.Add(sampleAdaptiveMs);
                                poolWaits.Add(samplePoolMs);
                                paceWaits.Add(samplePaceMs);
                                smtpSends.Add(sampleSmtpMs);'''
    if old not in t:
        raise SystemExit('latencies.Add not found')
    t = t.replace(old, new, 1)
    print('success samples added')

if 'AvgSmtpSendMs:' not in t:
    old = '''        return new MailTestResult(options.MessageCount, sent, failed, sw.Elapsed, lastError,
            avg, min, max, p50, p95, p99, throughput, cancelled, activeThroughput, retries, smtp4xx, smtp5xx, timeouts, poolConnections,
            adaptiveConcurrency, circuitOpen, (IReadOnlyList<string>)(mxRecords?.Select(r => r.Host).ToList() ?? new List<string>()));'''
    new = '''        return new MailTestResult(options.MessageCount, sent, failed, sw.Elapsed, lastError,
            avg, min, max, p50, p95, p99, throughput, cancelled, activeThroughput, retries, smtp4xx, smtp5xx, timeouts, poolConnections,
            adaptiveConcurrency, circuitOpen, (IReadOnlyList<string>)(mxRecords?.Select(r => r.Host).ToList() ?? new List<string>()),
            AvgPrepWaitMs: AverageOrZero(prepWaits),
            AvgAdaptiveWaitMs: AverageOrZero(adaptiveWaits),
            AvgPoolWaitMs: AverageOrZero(poolWaits),
            AvgPaceWaitMs: AverageOrZero(paceWaits),
            AvgSmtpSendMs: AverageOrZero(smtpSends));'''
    if old not in t:
        raise SystemExit('return MailTestResult not found')
    t = t.replace(old, new, 1)
    print('return updated')

if 'static double TicksToMs' not in t:
    marker = '    static MimeMessage BuildMessage('
    helper = '''    static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    static double AverageOrZero(ConcurrentBag<double> samples)
    {
        var arr = samples.ToArray();
        return arr.Length == 0 ? 0 : arr.Average();
    }

'''
    if marker not in t:
        raise SystemExit('BuildMessage not found')
    t = t.replace(marker, helper + marker, 1)
    print('helpers added')

p.write_text(t)
print('SmtpTestRunner written')
