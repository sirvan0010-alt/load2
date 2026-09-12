# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| **G** | **Security** | ✅ **SEC-001…004 FIXED** |
| **H** | **Concurrency / TLS** | ✅ **CLOSED (offline evidence)** — `docs/PHASE-H-EVIDENCE.md` |
| **I** | **Plugin / release** | ✅ **COMPLETE** |
| **Execution model** | BUG-001/003/007/009 | ✅ **FIXED** — PR #3 merge + CI #112 |

## Execution model (point 1)
- BUG-001 bounded workers: ✅
- BUG-003 first SEND immediate: ✅
- BUG-007 retry re-enters pacing: ✅
- BUG-009 exclusive actual-SEND gate (`SemaphoreSlim`): ✅
- Evidence: https://github.com/sirvan0010-alt/load2/pull/3 — CI #112 success on `main`

## Remaining
1. BUG-002 only if new ledger gap evidence (AutoRestart tests already green).
2. BUG-004 / BUG-005 / BUG-008 only with fresh defect evidence.
3. NET-AUDIT-001 only with real network endpoints.
4. Optional SEC-003 secret provenance scan.

## Safety boundary
Authorized, bounded mail-load tester only. Live send outside Test mode requires Unauthorized acknowledgement.
