#!/usr/bin/env python3
from pathlib import Path

# Models.cs
p = Path('src/MailLoadTester.Core/Models.cs')
t = p.read_text()
if 'string? RunId = null' not in t:
    old = '''    /// <summary>Average duration of the actual SmtpClient.SendAsync call.</summary>
    double AvgSmtpSendMs = 0);'''
    new = '''    /// <summary>Average duration of the actual SmtpClient.SendAsync call.</summary>
    double AvgSmtpSendMs = 0,
    /// <summary>FEAT-REPORT: unique id for this run (single attempt or aggregate AutoRestart).</summary>
    string? RunId = null,
    /// <summary>FEAT-HEALTH snapshots at end of run (may be empty).</summary>
    IReadOnlyList<EndpointHealthSnapshot>? EndpointHealth = null);'''
    if old not in t:
        raise SystemExit('Models tail not found')
    t = t.replace(old, new, 1)
    p.write_text(t)
    print('Models patched')
else:
    print('Models already has RunId')

# SmtpTestRunner
p = Path('src/MailLoadTester.Core/SmtpTestRunner.cs')
t = p.read_text()
if 'var runId = Guid.NewGuid()' not in t:
    old = '''        var endpointHealth = new TransportHealthRegistry();
        var endpointKey = $"{options.SmtpHost}:{options.Port}";'''
    new = '''        var endpointHealth = new TransportHealthRegistry();
        var endpointKey = $"{options.SmtpHost}:{options.Port}";
        var runId = Guid.NewGuid().ToString("N");
        var runStartedUtc = DateTimeOffset.UtcNow;'''
    if old not in t:
        raise SystemExit('endpoint health block missing')
    t = t.replace(old, new, 1)
    print('runId added')
if 'RunId: runId' not in t:
    old = 'AvgSmtpSendMs: AverageOrZero(smtpSends));'
    new = '''AvgSmtpSendMs: AverageOrZero(smtpSends),
            RunId: runId,
            EndpointHealth: endpointHealth.SnapshotAll());'''
    if old not in t:
        raise SystemExit('return AvgSmtpSendMs not found')
    t = t.replace(old, new, 1)
    print('return patched')
p.write_text(t)
print('done')
