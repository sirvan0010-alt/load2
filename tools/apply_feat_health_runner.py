#!/usr/bin/env python3
from pathlib import Path
p = Path('src/MailLoadTester.Core/SmtpTestRunner.cs')
t = p.read_text()
if 'endpointHealth.RecordSuccess' in t and 'endpointHealth.RecordFailure' in t:
    print('already wired')
    raise SystemExit(0)
if 'var endpointHealth = new TransportHealthRegistry()' not in t:
    old = '        var latencies = new ConcurrentBag<double>();'
    new = '''        var latencies = new ConcurrentBag<double>();
        // FEAT-HEALTH: per SMTP endpoint health (host:port). Independent of circuit breaker / proxy ban.
        var endpointHealth = new TransportHealthRegistry();
        var endpointKey = $"{options.SmtpHost}:{options.Port}";'''
    if old not in t:
        raise SystemExit('latencies not found')
    t = t.replace(old, new, 1)
if 'endpointHealth.RecordSuccess' not in t:
    old = '                                latencies.Add(msgSw.Elapsed.TotalMilliseconds);'
    new = '''                                latencies.Add(msgSw.Elapsed.TotalMilliseconds);
                                endpointHealth.RecordSuccess(endpointKey);'''
    if old not in t:
        raise SystemExit('latencies.Add not found')
    t = t.replace(old, new, 1)
if 'endpointHealth.RecordFailure' not in t:
    old = '''                            ledger.MarkFailed(i);
                            var f = Interlocked.Increment(ref failed);'''
    new = '''                            ledger.MarkFailed(i);
                            if (lastEx != null)
                                endpointHealth.RecordFailure(endpointKey, TransportHealthRegistry.ClassifyFailure(lastEx));
                            var f = Interlocked.Increment(ref failed);'''
    if old not in t:
        raise SystemExit('MarkFailed not found')
    t = t.replace(old, new, 1)
p.write_text(t)
print('runner health wired')
