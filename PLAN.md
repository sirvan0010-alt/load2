# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| **G** | **Security** | ✅ **SEC-001…004 FIXED** (path, X-headers, Unauthorized / `--unauthorized`, DryRun isolation) |
| H | Concurrency / TLS | 🟡 partial FIXED — residual audit evidence |
| **I** | **Plugin / release** | ✅ **COMPLETE** |

## Phase G — Security
- SEC-001: ✅
- SEC-002: ✅ path traversal + `X-*` custom headers
- SEC-003/004: ✅ `AuthorizationGate`, `MailTestOptions.Unauthorized`, Validation, GUI `BuildOptions`, CLI `--unauthorized`

## Phase I — Plugin / release
- I-1…I-7: ✅

## Remaining
1. Close Phase H residual audit evidence (no code change unless regression).
2. NET-AUDIT-001 only with real network evidence.
3. Final security + full regression CI on `main`.
4. Reconcile `BUGS-AUDIT.md` to evidence-backed state.

## Safety boundary
Authorized, bounded mail-load tester only. Live send outside Test mode requires explicit Unauthorized acknowledgement (GUI Start without Test mode, or CLI `--unauthorized`).
