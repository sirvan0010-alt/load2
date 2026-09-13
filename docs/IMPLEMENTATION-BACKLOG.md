# load2 — SMTP-first Implementation Backlog

**Authority:** source + tests on `main`.

## Done

| Item | Status |
|------|--------|
| Health / Report / Multi-account / Churn / Provider / Diagnostics | ✅ |
| TargetSet / ScenarioLimits / DurationSeconds | ✅ |
| GUI TransportDiagnostics on Test connection | ✅ |
| **GUI Accounts editor** | ✅ tab Účty SMTP + SmtpAccountMapping → `MailTestOptions.Accounts` |

## Next

1. **Per-target / per-provider throttling** (extend existing PerRecipientLimiter / SmartPace — no second limiter stack)
2. Bounded scenario queue
3. Retry / requeue with hard limits
4. Response classification unification
5. Global concurrency audit only if multi-pool overshoot proven

## GUI Accounts notes

- Empty list → classic single-host options path
- 2+ enabled accounts → SmtpAccountPoolHub
- Passwords never in RunReport JSON
