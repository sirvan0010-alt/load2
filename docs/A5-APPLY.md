# A5 apply — Models + SmtpTestRunner (small edits)

Foundation already on main:
- `SmtpOutcome.cs` / `SmtpOutcomeClassifier` / `SmtpOutcomeCounters`
- `RetryPolicy.IsRetryable` → classifier
- `TransportHealthRegistry.ClassifyFailure` → classifier
- unit tests `SmtpOutcomeClassifierTests`

## 1) Models.cs — one property on MailTestResult

Find:
```csharp
    RetryMetricsSnapshot? RetryMetrics = null);
```

Replace with:
```csharp
    RetryMetricsSnapshot? RetryMetrics = null,
    /// <summary>A5: unified SMTP outcome counts for this run.</summary>
    SmtpOutcomeCountsSnapshot? OutcomeCounts = null);
```

## 2) SmtpTestRunner.cs — three sites

### A) After retryMetrics init
```csharp
        var outcomeCounters = new SmtpOutcomeCounters();
```

### B) On success (after success = true)
```csharp
                                outcomeCounters.Record(SmtpOutcome.Success);
```

### C) On final failure (else branch, after MarkFailed path) when lastEx != null
Before or after MarkFailed:
```csharp
                            if (lastEx != null)
                                outcomeCounters.Record(lastEx);
                            else
                                outcomeCounters.Record(SmtpOutcome.Unknown);
```

### D) MailTestResult construction — add:
```csharp
            OutcomeCounts: outcomeCounters.Snapshot(),
```
next to RetryMetrics.

## A5 FIXED when
CI green after these wires (classifier + RetryPolicy + health already committed).
