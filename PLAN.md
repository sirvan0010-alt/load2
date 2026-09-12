# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| G | Security | ✅ SEC-001…004 + path/reparse; AUTH detector wiring CI #147 |
| H | Concurrency / TLS | ✅ offline evidence |
| **4** | **Combined concurrency stress** | ✅ adaptive+pace cancel · [CI #147](https://github.com/sirvan0010-alt/load2/actions/runs/34718413544) |
| I | Plugin / release | ✅ |

## Remaining (honest)
1. NET-AUDIT-001 live IP/proxy/IPv6 matrix — needs real endpoints.
2. Optional fake-SMTP AUTH LOGIN/PLAIN full transcript redaction.
3. Optional Phase 5 high-volume dry-run performance pass.

## Safety boundary
Authorized, bounded mail-load tester only.
