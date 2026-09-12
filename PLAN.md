# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| G | Security | 🟡 SEC-001/002 FIXED; **SEC-003/004 Core gate FIXED** (GUI MainForm wire-up may need manual upload) |
| H | Concurrency / TLS | 🟡 partial FIXED |
| **I** | **Plugin / release** | ✅ **COMPLETE** |

## Phase G — Security
- SEC-001: ✅
- SEC-002: ✅ path traversal + `X-*` custom headers
- SEC-003/004: ✅ `AuthorizationGate` + `MailTestOptions.Unauthorized` + Validation; CLI `--unauthorized`; DryRun remains network-free

## Phase I — Plugin / release
- I-1…I-7: ✅

## Remaining deferred
- NET-AUDIT-001 live source-IP/proxy/IPv6 matrix
- GUI `MainForm.BuildOptions` Unauthorized line (if not yet on main)
- Phase H residual audit evidence

## Safety boundary
Authorized, bounded mail-load tester only. Live send outside Test mode requires explicit Unauthorized acknowledgement.
