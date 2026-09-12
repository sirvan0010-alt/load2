# MailLoadTester (load2) — plán

**FIXED only with:** SHA + CI URL → `VERIFICATION.md`

| Phase | Topic | Status |
|-------|--------|--------|
| A | Execution model | ✅ FIXED |
| B | DeliveryLedger / AutoRestart | ✅ FIXED |
| C | Proxy BUG-004 | ✅ FIXED |
| D | IPv4 /31 /32 BUG-005 | ✅ FIXED — [CI #56](https://github.com/sirvan0010-alt/load2/actions/runs/34664949650) |
| E | Repo hygiene | 🟡 |
| **F** | **Direct MX BUG-008** | ⏳ **NEXT** |
| G | Security | ⏳ |
| H | Concurrency / TLS | ⏳ |
| I | Plugin / release | ⏳ |

## Next focus
`fix(mx): harden DirectMx against mixed recipient domains / validation bypass` (BUG-008)
