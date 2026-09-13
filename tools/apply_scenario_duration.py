#!/usr/bin/env python3
from pathlib import Path
import sys

# Models DurationSeconds
p = Path('src/MailLoadTester.Core/Models.cs')
t = p.read_text()
if 'DurationSeconds' not in t:
    old = '    IReadOnlyList<SmtpAccount>? Accounts = null);'
    new = '''    IReadOnlyList<SmtpAccount>? Accounts = null,
    /// <summary>Optional wall-clock limit for a run (seconds). 0 = disabled.</summary>
    int DurationSeconds = 0);'''
    if old not in t:
        sys.exit('Accounts marker missing')
    t = t.replace(old, new, 1)
    if 'DurationSeconds is < 0' not in t:
        marker = '        if (o.MessageCount is < 1 or > 10000)'
        idx = t.find(marker)
        end = t.find('\n', t.find('throw', idx)) + 1
        t = t[:end] + '        if (o.DurationSeconds is < 0 or > 86_400)\n            throw new ArgumentException("DurationSeconds musí být 0–86400.");\n' + t[end:]
    p.write_text(t)
    print('Models OK')
else:
    print('Models has DurationSeconds')

# Runner
p = Path('src/MailLoadTester.Core/SmtpTestRunner.cs')
t = p.read_text()
if 'durationCts' not in t:
    old = '        var fsm = new TestStateMachine();'
    t = t.replace(old, '        CancellationTokenSource? durationCts = null;\n        var fsm = new TestStateMachine();', 1)
    old_v = '''        try { Validation.Validate(options); }
        catch { fsm.ForceFailure("Validation failed"); throw; }
'''
    new_v = '''        try { Validation.Validate(options); }
        catch { fsm.ForceFailure("Validation failed"); throw; }

        if (options.DurationSeconds > 0)
        {
            durationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            durationCts.CancelAfter(TimeSpan.FromSeconds(options.DurationSeconds));
            ct = durationCts.Token;
        }
'''
    if old_v not in t:
        sys.exit('validate block missing')
    t = t.replace(old_v, new_v, 1)
    if 'durationCts?.Dispose()' not in t:
        t = t.replace('sessionLogger?.Dispose();', 'durationCts?.Dispose();\n            sessionLogger?.Dispose();', 1)
    p.write_text(t)
    print('Runner OK')
else:
    print('Runner has duration')

# GUI diagnostics
p = Path('src/MailLoadTester.Gui/MainForm.cs')
if p.exists():
    t = p.read_text()
    if 'TransportDiagnostics.RunAsync' not in t:
        old = '''            var options = BuildOptions();
            Validation.Validate(options);
            var result = await SmtpConnectivityTester.TestAsync(options, cts.Token);
            if (!cts.IsCancellationRequested && !_closeApproved && !IsDisposed)
                MessageBox.Show(result, "SMTP Test", MessageBoxButtons.OK, MessageBoxIcon.Information);'''
        new = '''            var options = BuildOptions();
            Validation.Validate(options);
            var report = await TransportDiagnostics.RunAsync(
                options,
                new TransportDiagnosticOptions(
                    CheckDnsPolicy: true,
                    CheckMx: false,
                    CheckSmtp: true,
                    TryAuthenticate: options.UseAuthentication,
                    DryRun: options.DryRun),
                cts.Token);
            var result = report.Summary + (string.IsNullOrEmpty(report.Error) ? "" : "\r\n\r\n" + report.Error);
            if (!cts.IsCancellationRequested && !_closeApproved && !IsDisposed)
                MessageBox.Show(result, "SMTP / diagnostika", MessageBoxButtons.OK,
                    report.Connected || options.DryRun ? MessageBoxIcon.Information : MessageBoxIcon.Warning);'''
        if old not in t:
            print('GUI block not found - skip')
        else:
            p.write_text(t.replace(old, new, 1))
            print('GUI OK')
    else:
        print('GUI already diagnostics')
