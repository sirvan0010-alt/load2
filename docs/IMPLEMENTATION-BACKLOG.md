# load2 — SMTP-first Implementation Backlog

**Authority:** source + tests on `main`.

## Done

| Item | Notes |
|------|-------|
| FEAT-HEALTH | TransportHealthRegistry |
| FEAT-REPORT | RunReport / RunId |
| Multi-account | Registry + PoolHub + runner branch |
| Connection churn | ConnectionChurnRunner |
| Provider presets | ApplyTo |
| TransportDiagnostics | DNS/MX/SMTP/EHLO |
| **TargetSet / ScenarioLimits / SmtpScenario** | validated targets + file load + PathSecurity |
| **DurationSeconds** | linked CTS in RunSingleAsync |
| **GUI Test connection** | uses TransportDiagnostics |

## Next

1. GUI Accounts editor (Accounts list → options)
2. Per-target / per-provider throttling in scenario layer
3. Queue/requeue with hard limits
4. Retry/classification expansion vs Health + Ledger
5. Global concurrency gate — only if audit proves multi-pool overshoot

## Out of scope for SMTP-first
SMS/WhatsApp/Call senders · public abuse endpoints · unrestricted flood
