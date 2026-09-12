# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| **G** | **Security** | ✅ **SEC-001…004 FIXED** |
| **H** | **Concurrency / TLS** | ✅ **CLOSED (offline evidence)** — see `docs/PHASE-H-EVIDENCE.md` |
| **I** | **Plugin / release** | ✅ **COMPLETE** |

## Phase H
- CONC-AUDIT-001/002: ✅ evidence on main (cross-component + cancellation tests)
- NET-AUDIT-002 offline TLS mapping/validation: ✅
- NET-AUDIT-001 live network matrix: ⏳ deferred (requires real endpoints)

## Remaining execution order
1. NET-AUDIT-001 only with real network evidence (never infer from source alone).
2. Optional: SEC-AUDIT-003 secret provenance scan; UNC/symlink path notes.
3. Final full regression CI on `main` (Release + all tests).
4. Reconcile `BUGS-AUDIT.md` to evidence-backed final state (do not blindly overwrite older claims).

## Safety boundary
Authorized, bounded mail-load tester only. Live send outside Test mode requires explicit Unauthorized acknowledgement.
