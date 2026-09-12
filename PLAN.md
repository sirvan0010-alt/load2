# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Bugs 001–009 (core) | ✅ FIXED |
| G | Security | 🟡 SEC-001 FIXED; SEC-002 partial tests; 003 open; 004 deferred |
| **H** | **Concurrency / TLS** | 🟡 matrix tests added (pending CI) |
| I | Plugin / release | ⏳ |

## Phase H coverage
- NET-AUDIT-002: `TlsMatrixTests` (socket options + port/security validation + DryRun isolation)
- CONC-AUDIT-001/002: `CrossComponentConcurrencyTests` + existing runner cancellation tests
- NET-AUDIT-001: still needs live source-IP/proxy/IPv6 matrix (deferred without network fixtures)

## Next
Green CI → mark H partial FIXED in VERIFICATION.md → Phase I release smoke / plugin audit
