using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace MailLoadTester;

/// <summary>
/// Options for the connection-churn scenario. Default send path remains persistent-session;
/// churn is an explicit opt-in stress mode over the existing pool/hub.
/// </summary>
public sealed record ConnectionChurnOptions(
    int MaxCycles = 10,
    int MaxDurationSeconds = 0,
    int MaxConcurrency = 1,
    bool ForceNewConnectionEachCycle = true,
    bool DryRun = false);

public sealed record ConnectionChurnResult(
    int RequestedCycles,
    int CompletedCycles,
    int ConnectSuccesses,
    int ConnectFailures,
    int Disconnects,
    int TransientFailures,
    int PermanentFailures,
    bool Cancelled,
    bool TimedOut,
    TimeSpan Elapsed,
    double AvgConnectMs,
    double MinConnectMs,
    double MaxConnectMs,
    int PoolCreatedCount,
    string LastError);

/// <summary>
/// Connection churn using existing SmtpConnectionPool / SmtpAccountPoolHub.
/// Does not replace the persistent-session send path in SmtpTestRunner.
/// </summary>
public static class ConnectionChurnRunner
{
    const int MaxCyclesHardCap = 100_000;
    const int MaxDurationHardCapSeconds = 86_400;

    public static void Validate(ConnectionChurnOptions o)
    {
        if (o.MaxCycles is < 1 or > MaxCyclesHardCap)
            throw new ArgumentException($"MaxCycles must be 1–{MaxCyclesHardCap}.");
        if (o.MaxDurationSeconds is < 0 or > MaxDurationHardCapSeconds)
            throw new ArgumentException($"MaxDurationSeconds must be 0–{MaxDurationHardCapSeconds}.");
        if (o.MaxConcurrency is < 1 or > 512)
            throw new ArgumentException("MaxConcurrency must be 1–512.");
    }

    public static async Task<ConnectionChurnResult> RunAsync(
        MailTestOptions baseOptions,
        ConnectionChurnOptions churn,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);
        ArgumentNullException.ThrowIfNull(churn);
        Validate(churn);

        var poolOptions = baseOptions with
        {
            MaxConcurrency = churn.MaxConcurrency,
            MessageCount = Math.Max(1, baseOptions.MessageCount),
            DryRun = churn.DryRun || baseOptions.DryRun
        };

        var connectMs = new ConcurrentBag<double>();
        var successes = 0;
        var failures = 0;
        var disconnects = 0;
        var transientFails = 0;
        var permanentFails = 0;
        var completed = 0;
        var cancelled = false;
        var timedOut = false;
        var lastError = "";
        var poolCreated = 0;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (churn.MaxDurationSeconds > 0)
            linked.CancelAfter(TimeSpan.FromSeconds(churn.MaxDurationSeconds));
        var workCt = linked.Token;

        var sw = Stopwatch.StartNew();
        try
        {
            if (poolOptions.DryRun)
            {
                await RunDryAsync(churn, workCt, connectMs,
                    () => Interlocked.Increment(ref successes),
                    () => Interlocked.Increment(ref completed),
                    () => Interlocked.Increment(ref disconnects)).ConfigureAwait(false);
            }
            else if (poolOptions.Accounts is { Count: > 1 })
            {
                var health = new TransportHealthRegistry();
                var registry = new SmtpAccountRegistry(poolOptions.Accounts);
                await using var hub = new SmtpAccountPoolHub(poolOptions, registry, health);
                await RunWorkersAsync(churn, workCt, async (token) =>
                {
                    var t0 = Stopwatch.GetTimestamp();
                    SmtpAccountLease? lease = null;
                    try
                    {
                        lease = await hub.RentAsync(token).ConfigureAwait(false);
                        var ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                        connectMs.Add(ms);
                        Interlocked.Increment(ref successes);
                        if (churn.ForceNewConnectionEachCycle)
                        {
                            lease.Discard();
                            Interlocked.Increment(ref disconnects);
                        }
                        else
                        {
                            lease.Return();
                        }
                        lease = null;
                    }
                    catch (OperationCanceledException)
                    {
                        lease?.Discard();
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lease?.Discard(ex);
                        Interlocked.Increment(ref failures);
                        Classify(ex, ref transientFails, ref permanentFails);
                        Volatile.Write(ref lastError, Validation.ExplainSmtpError(ex));
                    }
                    finally
                    {
                        Interlocked.Increment(ref completed);
                    }
                }).ConfigureAwait(false);

                foreach (var acc in registry.Snapshot())
                {
                    try { poolCreated += hub.GetOrCreatePool(acc).CreatedCount; }
                    catch (ObjectDisposedException) { break; }
                }
            }
            else
            {
                await using var pool = new SmtpConnectionPool(poolOptions);
                await RunWorkersAsync(churn, workCt, async (token) =>
                {
                    var t0 = Stopwatch.GetTimestamp();
                    MailKit.Net.Smtp.SmtpClient? client = null;
                    try
                    {
                        client = await pool.RentAsync(token).ConfigureAwait(false);
                        var ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                        connectMs.Add(ms);
                        Interlocked.Increment(ref successes);
                        if (churn.ForceNewConnectionEachCycle)
                        {
                            pool.Discard(client);
                            Interlocked.Increment(ref disconnects);
                            client = null;
                        }
                        else
                        {
                            pool.Return(client);
                            client = null;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (client != null) pool.Discard(client);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (client != null) pool.Discard(client);
                        Interlocked.Increment(ref failures);
                        Classify(ex, ref transientFails, ref permanentFails);
                        Volatile.Write(ref lastError, Validation.ExplainSmtpError(ex));
                    }
                    finally
                    {
                        Interlocked.Increment(ref completed);
                    }
                }).ConfigureAwait(false);
                poolCreated = pool.CreatedCount;
            }
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
                cancelled = true;
            else
                timedOut = true;
        }
        finally
        {
            sw.Stop();
        }

        if (workCt.IsCancellationRequested && !cancelled && !timedOut)
        {
            if (ct.IsCancellationRequested) cancelled = true;
            else if (churn.MaxDurationSeconds > 0) timedOut = true;
        }

        var samples = connectMs.ToArray();
        double avg = samples.Length > 0 ? samples.Average() : 0;
        double min = samples.Length > 0 ? samples.Min() : 0;
        double max = samples.Length > 0 ? samples.Max() : 0;

        return new ConnectionChurnResult(
            RequestedCycles: churn.MaxCycles,
            CompletedCycles: Volatile.Read(ref completed),
            ConnectSuccesses: Volatile.Read(ref successes),
            ConnectFailures: Volatile.Read(ref failures),
            Disconnects: Volatile.Read(ref disconnects),
            TransientFailures: Volatile.Read(ref transientFails),
            PermanentFailures: Volatile.Read(ref permanentFails),
            Cancelled: cancelled,
            TimedOut: timedOut,
            Elapsed: sw.Elapsed,
            AvgConnectMs: avg,
            MinConnectMs: min,
            MaxConnectMs: max,
            PoolCreatedCount: poolCreated,
            LastError: lastError);
    }

    static void Classify(Exception ex, ref int transientFails, ref int permanentFails)
    {
        if (ex is TimeoutException or IOException)
        {
            Interlocked.Increment(ref transientFails);
            return;
        }
        if (ex is MailKit.Net.Smtp.SmtpCommandException sce)
        {
            var code = (int)sce.StatusCode;
            if (code >= 400 && code < 500)
                Interlocked.Increment(ref transientFails);
            else
                Interlocked.Increment(ref permanentFails);
            return;
        }
        Interlocked.Increment(ref permanentFails);
    }

    static async Task RunDryAsync(
        ConnectionChurnOptions churn,
        CancellationToken ct,
        ConcurrentBag<double> connectMs,
        Action onSuccess,
        Action onComplete,
        Action onDisconnect)
    {
        await RunWorkersAsync(churn, ct, async (token) =>
        {
            token.ThrowIfCancellationRequested();
            var delay = Random.Shared.Next(1, 5);
            await Task.Delay(delay, token).ConfigureAwait(false);
            connectMs.Add(delay);
            onSuccess();
            if (churn.ForceNewConnectionEachCycle)
                onDisconnect();
            onComplete();
        }).ConfigureAwait(false);
    }

    static async Task RunWorkersAsync(
        ConnectionChurnOptions churn,
        CancellationToken ct,
        Func<CancellationToken, Task> cycleBody)
    {
        var workers = Math.Max(1, churn.MaxConcurrency);
        var capacity = Math.Min(churn.MaxCycles, workers * 2);
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(Math.Max(1, capacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        var producer = Task.Run(async () =>
        {
            try
            {
                for (var i = 0; i < churn.MaxCycles; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await channel.Writer.WriteAsync(i, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        var workerTasks = Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
        {
            await foreach (var _ in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                await cycleBody(ct).ConfigureAwait(false);
            }
        }, CancellationToken.None)).ToArray();

        try
        {
            await Task.WhenAll(workerTasks.Append(producer)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException(ct);
            throw;
        }
    }
}
