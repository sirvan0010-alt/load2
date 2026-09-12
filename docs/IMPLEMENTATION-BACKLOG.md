# load2 — SMTP-first Implementation Backlog

**Authority:** source + tests on `main`.

## Priority 0 — safety (unchanged)
Authorization, DryRun/TestMode, PathSecurity, AUTH redaction, bounded workers, pacing, ledger. No opaque MSBuild loaders. No force-push CI.

## Priority 1 — FEAT-HEALTH — **v1 DONE**

| Piece | Status |
|-------|--------|
| `TransportHealthRegistry` | ✅ `src/MailLoadTester.Core/TransportHealthRegistry.cs` |
| States Healthy / Degraded / Quarantined | ✅ |
| Thread-safe concurrent updates | ✅ tests |
| Failure classification | ✅ `ClassifyFailure` |
| Quarantine + success recovery + expiry | ✅ tests |
| Runner wire (`host:port` success/final fail) | ✅ `SmtpTestRunner` |
| CI | registry+tests [CI #189](https://github.com/sirvan0010-alt/load2/actions/runs/34724791426) |

**Already existed (not replaced):** CircuitBreaker, ProxyRotator ban, pool idle NOOP, 4xx/5xx counters.

**Still open for later health iterations:** multi-account selection from health, MX failover using `IsAvailable`, expose snapshots on `MailTestResult` / FEAT-REPORT.

## Priority 2 — FEAT-REPORT (next)
RunId, machine-readable summary, phase timings already on result (FEAT-022).

## Priority 3+ 
Multi-account session pool · connection-churn scenario · provider registry refinement · DNS/TLS diagnostics

## Not in SMTP-first scope
SMS/WhatsApp/Call senders · public OTP endpoints · CAPTCHA/OTP bypass · credential harvest · unrestricted DDoS
