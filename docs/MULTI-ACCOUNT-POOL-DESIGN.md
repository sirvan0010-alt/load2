# Multi-account SMTP session pool — design (from live main audit)

## What `SmtpConnectionPool` already is

- **One** `MailTestOptions` per pool instance (host/port/security/auth baked in at connect).
- `SemaphoreSlim(MaxConcurrency)` + idle queue + lease tracking.
- Persistent MailKit `SmtpClient`: connect → STARTTLS/SSL → AUTH once; `Return` reuses if connected.
- Idle NOOP health check; `Discard` on bad clients; proxy bind per client; IP rotation.
- Call sites: **only** `SmtpTestRunner` constructs the pool, `RentAsync` / `Return` / `Discard` / `PreWarmAsync` / `DisposeAsync`.

## What we do **not** do

- Do not fork a second connection implementation.
- Do not put passwords into `RunReport`.
- Do not force multi-account path when options still describe a single endpoint.

## Foundation landed

| Type | Role |
|------|------|
| `SmtpAccount` | Id + host/port/security/user/password; `HealthKey`; `FromOptions` / `ApplyTo` |
| `SmtpAccountRegistry` | Catalog + `TrySelect(health)` Healthy→Degraded, never Quarantined |

## Next implementation step (not done yet)

`SmtpAccountPoolHub`:

```text
base MailTestOptions (proxy, IP, timeouts, MaxConcurrency, …)
        +
SmtpAccountRegistry.TrySelect(TransportHealthRegistry)
        ↓
account.ApplyTo(baseOptions) → get-or-create SmtpConnectionPool(accountOptions)
        ↓
RentAsync / Return / Discard (existing pool API)
        ↓
ReportSuccess / ReportFailure on account.HealthKey
```

- One `SmtpConnectionPool` per `SmtpAccount.Id` (reuse persistent sessions per account).
- Global concurrency still enforced by runner workers + optional shared gate later.
- Runner stays single-pool until optional `Accounts` list is added to options/GUI.

## Selection policy (implemented in registry)

1. Prefer `Healthy`
2. Else `Degraded`
3. Never `Quarantined`
4. Round-robin within tier
5. All quarantined → `null` (caller fails fast / waits)
