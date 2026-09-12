using System.Collections.Concurrent;
using MailKit.Net.Smtp;

namespace MailLoadTester;

/// <summary>
/// Multi-account facade over existing <see cref="SmtpConnectionPool"/>.
/// One pool per <see cref="SmtpAccount.Id"/>; selection uses <see cref="SmtpAccountRegistry"/>
/// + <see cref="TransportHealthRegistry"/>. Does not replace single-account runner path.
/// </summary>
public sealed class SmtpAccountPoolHub : IAsyncDisposable
{
    private readonly MailTestOptions _baseOptions;
    private readonly SmtpAccountRegistry _registry;
    private readonly TransportHealthRegistry _health;
    private readonly SmtpSessionLogger? _sessionLogger;
    private readonly ProtocolPathObserver? _pathObserver;
    private readonly ConcurrentDictionary<string, SmtpConnectionPool> _pools =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _createLock = new();
    private int _disposed;

    public SmtpAccountPoolHub(
        MailTestOptions baseOptions,
        SmtpAccountRegistry registry,
        TransportHealthRegistry? health = null,
        SmtpSessionLogger? sessionLogger = null,
        ProtocolPathObserver? pathObserver = null)
    {
        _baseOptions = baseOptions ?? throw new ArgumentNullException(nameof(baseOptions));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _health = health ?? new TransportHealthRegistry();
        _sessionLogger = sessionLogger;
        _pathObserver = pathObserver;
    }

    public TransportHealthRegistry Health => _health;
    public SmtpAccountRegistry Registry => _registry;
    public int PoolCount => _pools.Count;

    public async Task<SmtpAccountLease> RentAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        var account = _registry.TrySelect(_health)
            ?? throw new InvalidOperationException(
                "No SMTP account available — all accounts are quarantined or the registry is empty.");

        var pool = GetOrCreatePool(account);
        var client = await pool.RentAsync(ct).ConfigureAwait(false);
        return new SmtpAccountLease(this, account, pool, client);
    }

    public SmtpConnectionPool GetOrCreatePool(SmtpAccount account)
    {
        ThrowIfDisposed();
        if (_pools.TryGetValue(account.Id, out var existing))
            return existing;

        lock (_createLock)
        {
            ThrowIfDisposed();
            if (_pools.TryGetValue(account.Id, out existing))
                return existing;

            var accountOptions = account.ApplyTo(_baseOptions);
            var pool = new SmtpConnectionPool(accountOptions, _sessionLogger, _pathObserver);
            if (!_pools.TryAdd(account.Id, pool))
            {
                _ = pool.DisposeAsync().AsTask();
                return _pools[account.Id];
            }
            return pool;
        }
    }

    internal void Return(SmtpAccountLease lease) => lease.Pool.Return(lease.Client);

    internal void Discard(SmtpAccountLease lease, Exception? transportError)
    {
        lease.Pool.Discard(lease.Client);
        if (transportError != null)
            _registry.ReportFailure(_health, lease.Account, transportError);
    }

    public void ReportSendSuccess(SmtpAccount account) =>
        _registry.ReportSuccess(_health, account);

    public void ReportSendFailure(SmtpAccount account, Exception ex) =>
        _registry.ReportFailure(_health, account, ex);

    void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(SmtpAccountPoolHub));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        foreach (var kv in _pools)
        {
            try { await kv.Value.DisposeAsync().ConfigureAwait(false); }
            catch { /* best effort */ }
        }
        _pools.Clear();
    }
}

/// <summary>
/// Rented client bound to a selected account. Dispose returns to pool;
/// call <see cref="SmtpAccountPoolHub.ReportSendSuccess"/> after successful Send.
/// </summary>
public sealed class SmtpAccountLease : IAsyncDisposable
{
    private readonly SmtpAccountPoolHub _hub;
    private int _released;

    internal SmtpAccountLease(
        SmtpAccountPoolHub hub,
        SmtpAccount account,
        SmtpConnectionPool pool,
        SmtpClient client)
    {
        _hub = hub;
        Account = account;
        Pool = pool;
        Client = client;
    }

    public SmtpAccount Account { get; }
    internal SmtpConnectionPool Pool { get; }
    public SmtpClient Client { get; }

    public void Return()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return;
        _hub.Return(this);
    }

    public void Discard(Exception? transportError = null)
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return;
        _hub.Discard(this, transportError);
    }

    public ValueTask DisposeAsync()
    {
        Return();
        return ValueTask.CompletedTask;
    }
}
