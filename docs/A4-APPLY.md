# A4 apply instructions

Foundation already on main (commit with RetryPolicy.cs + RetryMetrics.cs + tests).

## 1) Models.cs (Chrome edit)

Find:
```
    ScenarioQueueMetricsSnapshot? QueueMetrics = null);
```

Replace with:
```
    ScenarioQueueMetricsSnapshot? QueueMetrics = null,
    /// <summary>A4: retry/requeue observability for this run (null if unavailable).</summary>
    RetryMetricsSnapshot? RetryMetrics = null);
```

## 2) SmtpTestRunner.cs

Apply `docs/a4-runner.diff`:
```bash
git apply docs/a4-runner.diff
```
Or edit manually following the same diff (7 small sites).

## 3) Commit
```bash
git add src/MailLoadTester.Core/Models.cs src/MailLoadTester.Core/SmtpTestRunner.cs
git commit -m "feat(A4): wire RetryPolicy/RetryMetrics into SmtpTestRunner"
git push
```

## 4) A4 FIXED when CI green

A5 not started until A4 CI success.
