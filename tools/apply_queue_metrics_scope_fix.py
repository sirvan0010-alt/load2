#!/usr/bin/env python3
"""Fix queueMetrics CS0103: declare at RunSingleAsync method scope, not inside try."""
from pathlib import Path
import sys

p = Path('src/MailLoadTester.Core/SmtpTestRunner.cs')
t = p.read_text()

# Remove broken one-liner / misplaced declarations
t = t.replace('                     var queueMetrics = new ScenarioQueueMetrics();  ', '')
t = t.replace(
    'var queueMetrics = new ScenarioQueueMetrics();  for (int batchStart = 1;',
    'for (int batchStart = 1;')

# If already correctly placed early, stop
early = '        CancellationTokenSource? durationCts = null;\n        var queueMetrics = new ScenarioQueueMetrics();\n        var fsm = new TestStateMachine();'
if early in t and t.count('new ScenarioQueueMetrics()') == 1:
    print('already fixed')
    # still clean formatting below
else:
    # strip any remaining declarations
    while 'var queueMetrics = new ScenarioQueueMetrics();' in t:
        t = t.replace('        var queueMetrics = new ScenarioQueueMetrics();\n', '', 1)
        t = t.replace('            var queueMetrics = new ScenarioQueueMetrics();\n', '', 1)
        t = t.replace('                     var queueMetrics = new ScenarioQueueMetrics();\n', '', 1)

    old = '        CancellationTokenSource? durationCts = null;\n        var fsm = new TestStateMachine();'
    new = early
    if old not in t:
        sys.exit('durationCts/fsm marker missing')
    t = t.replace(old, new, 1)
    print('method-scope declaration added')

# Ensure bare for has indent
t = t.replace(
    '\nfor (int batchStart = 1; batchStart <= options.MessageCount; batchStart += options.BatchMode ? options.BatchSize : options.MessageCount)\n            {',
    '\n            for (int batchStart = 1; batchStart <= options.MessageCount; batchStart += options.BatchMode ? options.BatchSize : options.MessageCount)\n            {')

# finally indent
t = t.replace(
'''                finally
                {
                    workChannel.Writer.TryComplete();
                while (workChannel.Reader.TryRead(out _))
                        queueMetrics.RecordDrained();
                }
''',
'''                finally
                {
                    workChannel.Writer.TryComplete();
                    while (workChannel.Reader.TryRead(out _))
                        queueMetrics.RecordDrained();
                }
''')

t = t.replace(
'''                    try
                    {
                                               await foreach (var i in workChannel.Reader.ReadAllAsync(workerCt).ConfigureAwait(false))
                        {
''',
'''                    try
                    {
                        await foreach (var i in workChannel.Reader.ReadAllAsync(workerCt).ConfigureAwait(false))
                        {
''')

t = t.replace(
'''                        // Workers must surface user cancel to the run result (BUG 1.5).
              cancelled = true;
''',
'''                        // Workers must surface user cancel to the run result (BUG 1.5).
                        cancelled = true;
''')

t = t.replace(
'''                try
                {
                        for (var i = batchStart; i <= batchEnd; i++)
                    {
''',
'''                try
                {
                    for (var i = batchStart; i <= batchEnd; i++)
                    {
''')

if 'QueueMetrics: queueMetrics.Snapshot()' not in t:
    sys.exit('QueueMetrics return missing')
if t.count('new ScenarioQueueMetrics()') != 1:
    sys.exit(f"expected 1 declaration, got {t.count('new ScenarioQueueMetrics()')}")

p.write_text(t)
print('OK')
