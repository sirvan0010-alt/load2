using MailKit.Net.Smtp;
using MailKit.Security;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace MailLoadTester;

public sealed class SmtpConnectionPool : IAsyncDisposable
{
    private readonly ConcurrentQueue<SmtpClient> _idle = new();
    private readonly ConcurrentDictionary<SmtpClient, byte> _all = new();
    private readonly ConcurrentDictionary<SmtpClient, byte> _leased = new();
    private readonly ConcurrentDictionary<SmtpClient, long> _idleSince = new();
    private readonly SemaphoreSlim _gate;
    private readonly MailTestOptions _options;
    private readonly SecureSocketOptions _socket;
    private readonly IPEndPoint? _localEp;
    private readonly IpV6Rotator? _ipv6Rotator;
    private readonly IpV4Rotator? _ipv4Rotator;
    private readonly bool _ipv4Random;
    private readonly ProxyRotator? _proxyRotator;
    private readonly ConcurrentDictionary<SmtpClient, ProxyEndpoint> _clientProxy = new();
    private readonly System.Security.Cryptography.X509Certificates.X509Certificate2? _clientCert;
    private readonly SmtpSessionLogger? _sessionLogger;
    private readonly ProtocolPathObserver? _pathObserverTemplate;
    private readonly ConcurrentDictionary<SmtpClient, ProtocolPathObserver> _pathObservers = new();
    private int _created;
    private int _disposed;
    // Counts RentAsync calls currently inside EnsureConnectedAsync for a brand-new
    // client — the window where _clientCert is attached to client.ClientCertificates
    // and actively used for the mTLS handshake, but before the client is visible in
    // _leased (MarkLeased only runs after EnsureConnectedAsync succeeds). DisposeAsync
    // must wait for this to drain to 0 before disposing _clientCert, or it could
    // dispose the certificate out from under an in-flight handshake on another thread.
    private int _inFlightConnects;
    private readonly object _lifecycleLock = new();

    public SmtpConnectionPool(MailTestOptions options, SmtpSessionLogger? sessionLogger = null, ProtocolPathObserver? pathObserver = null)
    {
        _options = options;
        _socket = SmtpConnectivityTester.ToSocketOptions(options.Security);
        _gate = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
        _sessionLogger = sessionLogger;
        _pathObserverTemplate = pathObserver;
        _localEp = IpBindingHelper.ResolveLocalEndPoint(options.SourceIp, options.IpVersion);
        _ipv6Rotator = !string.IsNullOrWhiteSpace(options.Ipv6Prefix)
            ? new IpV6Rotator(options.Ipv6Prefix, options.Ipv6PrefixLength <= 0 ? 64 : options.Ipv6PrefixLength)
            : null;
        _ipv4Rotator = !string.IsNullOrWhiteSpace(options.Ipv4Rotation)
            ? new IpV4Rotator(options.Ipv4Rotation)
            : null;
        _ipv4Random = options.Ipv4RotationRandom;
        if (!string.IsNullOrWhiteSpace(options.ProxyList))
        {
            var list = ProxyClientFactory.ParseList(options.ProxyList);
            if (list.Count > 0)
                _proxyRotator = new ProxyRotator(list, options.ProxyListRandom, TimeSpan.FromMinutes(Math.Max(1, options.ProxyBanMinutes)));
        }
        else if (options.UseSocks5Proxy && !string.IsNullOrWhiteSpace(options.ProxyHost))
        {
            var single = new ProxyEndpoint(ProxyType.Socks5, options.ProxyHost, options.ProxyPort,
                string.IsNullOrEmpty(options.ProxyUsername) ? null : options.ProxyUsername,
                string.IsNullOrEmpty(options.ProxyPassword) ? null : options.ProxyPassword);
            _proxyRotator = new ProxyRotator(new[] { single }, false, TimeSpan.FromMinutes(Math.Max(1, options.ProxyBanMinutes)));
        }
        _clientCert = ClientCertificateHelper.Load(options.ClientCertificatePath, options.ClientCertificatePassword);
    }

    public async Task PreWarmAsync(int count, CancellationToken ct)
    {
        count = Math.Min(count, _options.MaxConcurrency);
        var tasks = new List<Task<SmtpClient>>(count);
        for (int i = 0; i < count; i++)
            tasks.Add(RentAsync(ct));

        try
        {
            var clients = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var c in clients)
                Return(c);
        }
        catch
        {
            foreach (var task in tasks)
            {
                if (task.Status == TaskStatus.RanToCompletion)
                    Return(task.Result);
            }
            throw;
        }
    }

    public async Task<SmtpClient> RentAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(SmtpConnectionPool));

            while (_idle.TryDequeue(out var idleClient))
            {
                _idleSince.TryRemove(idleClient, out var idleSince);

                if (!idleClient.IsConnected)
                {
                    try
                    {
                        await EnsureConnectedAsync(idleClient, ct).ConfigureAwait(false);
                        if (Volatile.Read(ref _disposed) != 0)
                        {
                            Forget(idleClient);
                            throw new ObjectDisposedException(nameof(SmtpConnectionPool));
                        }
                        MarkLeased(idleClient);
                        return idleClient;
                    }
                    catch (OperationCanceledException) { Forget(idleClient); throw; }
                    catch (ObjectDisposedException) { Forget(idleClient); throw; }
                    catch (Exception ex)
                    {
                        _sessionLogger?.LogInfo($"Idle client {idleClient.GetHashCode()} reconnect failed, discarding: {ex.Message}");
                        Forget(idleClient);
                    }
                }

                if (idleClient.IsConnected)
                {
                    var shouldHealthCheck = false;
                    if (_options.IdleConnectionHealthCheckSeconds > 0 && idleSince > 0)
                    {
                        var idleSeconds = (Stopwatch.GetTimestamp() - idleSince) /
                            (double)Stopwatch.Frequency;
                        shouldHealthCheck = idleSeconds >= _options.IdleConnectionHealthCheckSeconds;
                    }

                    if (shouldHealthCheck)
                    {
                        try
                        {
                            await idleClient.NoOpAsync(ct).ConfigureAwait(false);
                            _sessionLogger?.LogInfo($"Idle health-check OK for client {idleClient.GetHashCode()}");
                        }
                        catch (OperationCanceledException)
                        {
                            Forget(idleClient);
                            throw;
                        }
                        catch
                        {
                            _sessionLogger?.LogInfo($"Idle health-check failed for client {idleClient.GetHashCode()}, discarding");
                            Forget(idleClient);
                            continue;
                        }
                    }

                    bool shutdown;
                    lock (_lifecycleLock)
                    {
                        shutdown = Volatile.Read(ref _disposed) != 0;
                        if (!shutdown)
                        {
                            _sessionLogger?.LogInfo($"Reusing idle client {idleClient.GetHashCode()}");
                            MarkLeased(idleClient);
                        }
                    }

                    if (shutdown)
                    {
                        Forget(idleClient);
                        throw new ObjectDisposedException(nameof(SmtpConnectionPool));
                    }

                    return idleClient;
                }
            }

            ProtocolPathObserver? clientPathObserver = _pathObserverTemplate != null
                ? new ProtocolPathObserver()
                : null;
            MailKit.IProtocolLogger? logger = null;
            if (_sessionLogger != null && clientPathObserver != null)
                logger = new CompositeProtocolLogger(new SessionProtocolLogger(_sessionLogger), clientPathObserver);
            else if (_sessionLogger != null)
                logger = new SessionProtocolLogger(_sessionLogger);
            else if (clientPathObserver != null)
                logger = clientPathObserver;

            var client = logger != null ? new SmtpClient(logger) : new SmtpClient();
            var clientOwnedByPool = false;
            try
            {
                if (clientPathObserver != null)
                    _pathObservers[client] = clientPathObserver;
                client.Timeout = Math.Max(_options.ConnectTimeoutMs, _options.ReadTimeoutMs);
                if (_options.IgnoreCertificateErrors)
                    client.ServerCertificateValidationCallback = (_, _, _, _) => true;
                if (_clientCert != null)
                    client.ClientCertificates.Add(_clientCert!);

                if (_proxyRotator != null)
                {
                    var ep = _proxyRotator.TryGetNext();
                    if (ep is null)
                        throw new InvalidOperationException("Všechny proxy jsou dočasně vyřazené (ban detection).");
                    var proxyClient = ProxyClientFactory.Create(ep);
                    if (proxyClient != null)
                    {
                        client.ProxyClient = proxyClient;
                        _clientProxy[client] = ep;
                        _sessionLogger?.LogInfo($"Proxy {ep.DisplayKey}");
                    }
                }

                _sessionLogger?.LogInfo($"Creating new connection to {_options.SmtpHost}:{_options.Port}");
                await EnsureConnectedAsync(client, ct).ConfigureAwait(false);
                _sessionLogger?.LogInfo("Connected and authenticated");

                lock (_lifecycleLock)
                {
                    if (Volatile.Read(ref _disposed) != 0)
                        throw new ObjectDisposedException(nameof(SmtpConnectionPool));

                    _all.TryAdd(client, 0);
                    MarkLeased(client);
                    Interlocked.Increment(ref _created);
                    clientOwnedByPool = true;
                    return client;
                }
            }
            catch (Exception ex)
            {
                if (!clientOwnedByPool)
                {
                    _sessionLogger?.LogInfo($"Connection failed: {ex.Message}");
                    _pathObservers.TryRemove(client, out _);
                    _clientProxy.TryRemove(client, out _);
                    await SafeDisposeAsync(client).ConfigureAwait(false);
                }
                throw;
            }
        }
        catch
        {
            _gate.Release();
            throw;
        }
    }

    public void ReportProxyBlocked(SmtpClient client)
    {
        if (_proxyRotator is null) return;
        if (_clientProxy.TryGetValue(client, out var ep))
            _proxyRotator.ReportBlocked(ep);
    }

    public ProxyEndpoint? GetClientProxy(SmtpClient client) =>
        _clientProxy.TryGetValue(client, out var ep) ? ep : null;

    public IReadOnlyList<(DeliveryStepKind Step, bool? Ok, string Detail)> DrainPathEvents(SmtpClient client)
        => _pathObservers.TryGetValue(client, out var observer)
            ? observer.Drain()
            : Array.Empty<(DeliveryStepKind, bool?, string)>();

    public void Return(SmtpClient client)
    {
        if (!_leased.TryRemove(client, out _))
            return;

        bool returnToIdle;
        lock (_lifecycleLock)
        {
            returnToIdle = Volatile.Read(ref _disposed) == 0 && client.IsConnected;
            if (returnToIdle)
            {
                _idleSince[client] = Stopwatch.GetTimestamp();
                _idle.Enqueue(client);
            }
        }

        if (!returnToIdle)
            Forget(client);

        _gate.Release();
    }

    public void Discard(SmtpClient client)
    {
        if (!_leased.TryRemove(client, out _))
            return;
        Forget(client);
        _gate.Release();
    }

    private void MarkLeased(SmtpClient client) => _leased[client] = 0;

    void Forget(SmtpClient client)
    {
        _all.TryRemove(client, out _);
        _leased.TryRemove(client, out _);
        _idleSince.TryRemove(client, out _);
        if (_pathObservers.TryRemove(client, out var observer))
            observer.Clear();
        _clientProxy.TryRemove(client, out _);
        _ = SafeDisposeAsync(client);
    }

    async Task EnsureConnectedAsync(SmtpClient client, CancellationToken ct)
    {
        if (!client.IsConnected)
        {
            lock (_lifecycleLock)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    throw new ObjectDisposedException(nameof(SmtpConnectionPool));
                _inFlightConnects++;
            }
            try
            {
                if (_ipv6Rotator != null)
                {
                    var ip = _ipv6Rotator.GetNextRandomIp();
                    var ep = new IPEndPoint(ip, 0);
                    var socket = IpBindingHelper.CreateBoundSocket(ep, IpVersionPreference.IPv6Only);
                    await client.ConnectAsync(socket, _options.SmtpHost, _options.Port, _socket, ct).ConfigureAwait(false);
                }
                else if (_ipv4Rotator != null)
                {
                    var ip = _ipv4Random ? _ipv4Rotator.GetRandomIp() : _ipv4Rotator.GetNextIp();
                    var ep = new IPEndPoint(ip, 0);
                    var socket = IpBindingHelper.CreateBoundSocket(ep, IpVersionPreference.IPv4Only);
                    await client.ConnectAsync(socket, _options.SmtpHost, _options.Port, _socket, ct).ConfigureAwait(false);
                }
                else if (_localEp != null)
                {
                    var socket = IpBindingHelper.CreateBoundSocket(_localEp, _options.IpVersion);
                    await client.ConnectAsync(socket, _options.SmtpHost, _options.Port, _socket, ct).ConfigureAwait(false);
                }
                else
                {
                    await client.ConnectAsync(_options.SmtpHost, _options.Port, _socket, ct).ConfigureAwait(false);
                }

                if (_options.UseAuthentication)
                {
                    if (_options.AuthMethod == SmtpAuthMethod.Auto)
                        await client.AuthenticateAsync(_options.Username, _options.Password, ct).ConfigureAwait(false);
                    else
                    {
                        var sasl = AuthMethodHelper.CreateSasl(_options.AuthMethod, _options.Username, _options.Password);
                        await client.AuthenticateAsync(sasl, ct).ConfigureAwait(false);
                    }
                    _sessionLogger?.LogInfo("Authentication succeeded");
                }
            }
            finally
            {
                lock (_lifecycleLock)
                {
                    _inFlightConnects--;
                }
            }
        }
        else if (_options.UseAuthentication && !client.IsAuthenticated)
        {
            if (_options.AuthMethod == SmtpAuthMethod.Auto)
                await client.AuthenticateAsync(_options.Username, _options.Password, ct).ConfigureAwait(false);
            else
            {
                var sasl = AuthMethodHelper.CreateSasl(_options.AuthMethod, _options.Username, _options.Password);
                await client.AuthenticateAsync(sasl, ct).ConfigureAwait(false);
            }
        }
    }

    static async Task SafeDisposeAsync(SmtpClient client)
    {
        try { if (client.IsConnected) await client.DisconnectAsync(false).ConfigureAwait(false); }
        catch { }
        try { client.Dispose(); }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_lifecycleLock)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            Volatile.Write(ref _disposed, 1);
        }

        while (_idle.TryDequeue(out var c))
        {
            _all.TryRemove(c, out _);
            _leased.TryRemove(c, out _);
            _idleSince.TryRemove(c, out _);
            if (_pathObservers.TryRemove(c, out var observer)) observer.Clear();
            await SafeDisposeAsync(c).ConfigureAwait(false);
        }

        while (Volatile.Read(ref _inFlightConnects) > 0)
            await Task.Delay(10).ConfigureAwait(false);

        _clientCert?.Dispose();
    }

    public int CreatedCount => Volatile.Read(ref _created);
}
