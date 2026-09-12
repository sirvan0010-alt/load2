# Multi-account SMTP session pool

## Status on main

| Piece | Status |
|-------|--------|
| `SmtpAccount` / `SmtpAccountRegistry` | ✅ |
| `SmtpAccountPoolHub` / `SmtpAccountLease` | ✅ |
| `MailTestOptions.Accounts` | ✅ |
| Runner branch `Count > 1` → hub | ✅ |
| Single-account path (`null` / 0 / 1) | ✅ unchanged |
| Dry-run tests | ✅ `MultiAccountRunnerTests` |

## Semantics

```text
Accounts == null || Count <= 1
  → SmtpConnectionPool(options)   // classic path

Accounts.Count > 1
  → SmtpAccountRegistry + SmtpAccountPoolHub
  → shared TransportHealthRegistry (EndpointHealth snapshots)
  → per-account persistent SmtpConnectionPool
  → ReportSendSuccess / Discard(ex) → account HealthKey
```

## Invariants

- Passwords never enter `RunReport` JSON.
- PreWarm (multi): each account pool pre-warmed.
- PoolConnections (multi): sum of `CreatedCount` before hub dispose.
- AutoRestart: still outer `RunAsync` loop; each attempt creates a new hub/pool as today.

## Not done

- GUI editor for account list
- Integration test with real multi-host fake SMTP
- Shared global concurrency gate across account pools (each pool still uses MaxConcurrency)
