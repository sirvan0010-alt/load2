# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| **G** | **Security** | ✅ **SEC-001…004 + path/reparse + deps** |
| **H** | **Concurrency / TLS** | ✅ offline evidence closed |
| **I** | **Plugin / release** | ✅ COMPLETE |
| **Execution model** | BUG-001/003/007/009 | ✅ FIXED (PR #3 + CI) |

## Security (G) detail
- SEC-001 AUTH redaction: ✅
- SEC-002 path `..` + `X-*` headers: ✅
- SEC-003 secret provenance scan: ✅ `docs/SEC-003-SECRET-PROVENANCE.md`
- SEC-004 `--unauthorized`: ✅
- Reparse-point / PathSecurity on Validation + ProfileStore + EML/attachments: ✅
- MailKit/MimeKit 4.17.0 (NU1902): ✅
- CodeQL on main: ✅

## Remaining (honest)
1. NET-AUDIT-001 live IP/proxy/IPv6 matrix — requires real endpoints.
2. BUG-004/005/008 only with fresh defect evidence.
3. Optional: durable profile encryption at rest (out of current scope).

## Safety boundary
Authorized, bounded mail-load tester only.
