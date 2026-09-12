#!/usr/bin/env python3
from pathlib import Path
import sys

# Models
p = Path('src/MailLoadTester.Core/Models.cs')
t = p.read_text()
if 'IReadOnlyList<SmtpAccount>? Accounts' not in t:
    old = '    bool Unauthorized = false);'
    new = '''    bool Unauthorized = false,
    /// <summary>
    /// Optional multi-account list. Null/empty/single-entry keeps the classic single-pool path
    /// (Username/Password on options). Two or more entries activate SmtpAccountPoolHub.
    /// </summary>
    IReadOnlyList<SmtpAccount>? Accounts = null);'''
    if old not in t:
        sys.exit('Models Unauthorized marker missing')
    t = t.replace(old, new, 1)
marker = '''        if (!o.DryRun && !o.TestMode && !o.Unauthorized)
            throw new ArgumentException(
                "Odesílání mimo Test mode vyžaduje --unauthorized (nebo Unauthorized=true).");'''
if 'o.Accounts is { Count: > 0 }' not in t:
    accounts_val = marker + '''
        if (o.Accounts is { Count: > 0 })
        {
            foreach (var a in o.Accounts)
            {
                if (a is null || string.IsNullOrWhiteSpace(a.Id))
                    throw new ArgumentException("Každý SmtpAccount musí mít neprázdné Id.");
                if (string.IsNullOrWhiteSpace(a.SmtpHost))
                    throw new ArgumentException($"SmtpAccount '{a?.Id}' musí mít SmtpHost.");
                if (a.UseAuthentication && string.IsNullOrWhiteSpace(a.Username))
                    throw new ArgumentException($"SmtpAccount '{a.Id}' vyžaduje Username.");
                if (a.UseAuthentication && string.IsNullOrEmpty(a.Password) && a.AuthMethod != SmtpAuthMethod.OAuth2)
                    throw new ArgumentException($"SmtpAccount '{a.Id}' vyžaduje Password (kromě OAuth2).");
            }
        }'''
    if marker not in t:
        sys.exit('unauthorized validation marker missing')
    t = t.replace(marker, accounts_val, 1)
p.write_text(t)
print('Models OK')

# Runner
p = Path('src/MailLoadTester.Core/SmtpTestRunner.cs')
t = p.read_text()
if 'var multiAccount = options.Accounts' in t:
    print('Runner already multi-account')
    raise SystemExit(0)

old = '''        SmtpConnectionPool? pool = null;
        try
        {
            if (!options.DryRun)
            {
                pool = new SmtpConnectionPool(options, sessionLogger, pathObserver);
                if (options.PreWarmConnections)
                {
                    Report(0, 0, "Pre-warming SMTP connections…", null, 2, "Pre-warm spojení", "Odesílání");
                    await pool.PreWarmAsync(options.MaxConcurrency, ct).ConfigureAwait(false);
                    Report(0, 0, $"Pre-warmed {pool.CreatedCount} connections", null, 2, "Spojení připravena", "Odesílání");
                }
            }'''

new = '''        SmtpConnectionPool? pool = null;
        SmtpAccountPoolHub? accountHub = null;
        var multiPoolConnections = 0;
        // N>1 accounts → hub; otherwise classic single SmtpConnectionPool (unchanged semantics).
        var multiAccount = options.Accounts is { Count: > 1 };
        try
        {
            if (!options.DryRun)
            {
                if (multiAccount)
                {
                    var accountRegistry = new SmtpAccountRegistry(options.Accounts!);
                    accountHub = new SmtpAccountPoolHub(options, accountRegistry, endpointHealth, sessionLogger, pathObserver);
                    if (options.PreWarmConnections)
                    {
                        Report(0, 0, "Pre-warming multi-account SMTP pools…", null, 2, "Pre-warm spojení", "Odesílání");
                        var warmed = 0;
                        foreach (var acc in accountRegistry.Snapshot())
                        {
                            var ap = accountHub.GetOrCreatePool(acc);
                            await ap.PreWarmAsync(options.MaxConcurrency, ct).ConfigureAwait(false);
                            warmed += ap.CreatedCount;
                        }
                        Report(0, 0, $"Pre-warmed {warmed} connections across {accountRegistry.Count} accounts", null, 2, "Spojení připravena", "Odesílání");
                    }
                }
                else
                {
                    pool = new SmtpConnectionPool(options, sessionLogger, pathObserver);
                    if (options.PreWarmConnections)
                    {
                        Report(0, 0, "Pre-warming SMTP connections…", null, 2, "Pre-warm spojení", "Odesílání");
                        await pool.PreWarmAsync(options.MaxConcurrency, ct).ConfigureAwait(false);
                        Report(0, 0, $"Pre-warmed {pool.CreatedCount} connections", null, 2, "Spojení připravena", "Odesílání");
                    }
                }
            }'''
if old not in t:
    sys.exit('pool setup block not found')
t = t.replace(old, new, 1)

if 'SmtpAccountLease? accountLease' not in t:
    if 'SmtpClient? client = null;' not in t:
        sys.exit('client=null not found')
    t = t.replace(
        'SmtpClient? client = null;',
        'SmtpClient? client = null;\n                        SmtpAccountLease? accountLease = null;',
        1)

old_rent = '''                                    var poolT0 = Stopwatch.GetTimestamp();
                                    client = await pool!.RentAsync(workerCt).ConfigureAwait(false);
                                    samplePoolMs = TicksToMs(Stopwatch.GetTimestamp() - poolT0);
                                    ReportPathEvents(Volatile.Read(ref sent), Volatile.Read(ref failed), i, workerId, client, pool!);'''
new_rent = '''                                    var poolT0 = Stopwatch.GetTimestamp();
                                    if (multiAccount)
                                    {
                                        accountLease = await accountHub!.RentAsync(workerCt).ConfigureAwait(false);
                                        client = accountLease.Client;
                                        samplePoolMs = TicksToMs(Stopwatch.GetTimestamp() - poolT0);
                                        ReportPathEvents(Volatile.Read(ref sent), Volatile.Read(ref failed), i, workerId, client, accountLease.Pool);
                                    }
                                    else
                                    {
                                        client = await pool!.RentAsync(workerCt).ConfigureAwait(false);
                                        samplePoolMs = TicksToMs(Stopwatch.GetTimestamp() - poolT0);
                                        ReportPathEvents(Volatile.Read(ref sent), Volatile.Read(ref failed), i, workerId, client, pool!);
                                    }'''
if old_rent not in t:
    sys.exit('rent block not found')
t = t.replace(old_rent, new_rent, 1)

t = t.replace(
    '''                                    pool.Return(client);
                                    client = null;''',
    '''                                    if (accountLease != null)
                                    {
                                        accountHub!.ReportSendSuccess(accountLease.Account);
                                        accountLease.Return();
                                        accountLease = null;
                                    }
                                    else
                                        pool!.Return(client);
                                    client = null;''')

for disc in ['pool!.Discard(client);', 'pool.Discard(client);']:
    t = t.replace(
        disc,
        '''if (accountLease != null)
                                    {
                                        accountLease.Discard(ex);
                                        accountLease = null;
                                    }
                                    else
                                        pool!.Discard(client);''')

t = t.replace(
    'pool!.ReportProxyBlocked(client);',
    '(accountLease != null ? accountLease.Pool : pool)!.ReportProxyBlocked(client);')

old_hs = '                                endpointHealth.RecordSuccess(endpointKey);'
if old_hs in t:
    t = t.replace(old_hs, '''                                if (!multiAccount)
                                    endpointHealth.RecordSuccess(endpointKey);''', 1)

old_hf = '''                            if (lastEx != null)
                                endpointHealth.RecordFailure(endpointKey, TransportHealthRegistry.ClassifyFailure(lastEx));'''
if old_hf in t:
    t = t.replace(old_hf, '''                            if (lastEx != null && !multiAccount)
                                endpointHealth.RecordFailure(endpointKey, TransportHealthRegistry.ClassifyFailure(lastEx));''', 1)

old_oce = '''                            catch (OperationCanceledException)
                            {
                                if (client is not null) pool?.Discard(client);
                                throw;
                            }'''
new_oce = '''                            catch (OperationCanceledException)
                            {
                                if (client is not null)
                                {
                                    if (accountLease != null)
                                    {
                                        accountLease.Discard();
                                        accountLease = null;
                                    }
                                    else
                                        pool?.Discard(client);
                                    client = null;
                                }
                                throw;
                            }'''
if old_oce in t:
    t = t.replace(old_oce, new_oce, 1)

old_fin = '''            if (pool != null)
                await pool.DisposeAsync().ConfigureAwait(false);'''
new_fin = '''            if (accountHub != null)
            {
                foreach (var acc in accountHub.Registry.Snapshot())
                {
                    try { multiPoolConnections += accountHub.GetOrCreatePool(acc).CreatedCount; }
                    catch (ObjectDisposedException) { break; }
                }
                await accountHub.DisposeAsync().ConfigureAwait(false);
            }
            if (pool != null)
                await pool.DisposeAsync().ConfigureAwait(false);'''
if old_fin not in t:
    sys.exit('dispose not found')
t = t.replace(old_fin, new_fin, 1)

t = t.replace(
    '        var poolConnections = pool?.CreatedCount ?? 0;',
    '        var poolConnections = multiAccount ? multiPoolConnections : (pool?.CreatedCount ?? 0);',
    1)

p.write_text(t)
print('Runner OK')
