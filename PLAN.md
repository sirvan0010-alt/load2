# MailLoadTester (load2) — plán práce

**Source of truth:** `PLAN.md` + `VERIFICATION.md` + `BUGS-AUDIT.md`  
**FIXED only with:** commit SHA + CI run URL

## Status board

| Phase | Topic | Status |
|-------|--------|--------|
| **0** | CI / quarantine | ✅ |
| **A** | Execution model (001/003/007/009) | ✅ FIXED |
| **B** | DeliveryLedger / AutoRestart (002) | ✅ FIXED |
| **C** | Proxy rotation (004) | ✅ FIXED — [CI #54](https://github.com/sirvan0010-alt/load2/actions/runs/34664736578) |
| **D** | IPv4 /31 /32 (005) | ⏳ **NEXT** |
| **E** | Repo integrity (006) | 🟡 patch workflows removed |
| **F** | Direct MX (008) | ⏳ |
| **G** | Security | ⏳ |
| **H** | Concurrency / TLS | ⏳ |
| **I** | Plugin / release | ⏳ last |

## AI rules

1. One BUG per change set  
2. `dotnet build` + `dotnet test` Release, no weakened asserts  
3. FIXED only after green CI + `VERIFICATION.md` entry  
4. No drive-by refactors  

## Next commit focus

`fix(ipv4): RFC 3021 /31 both usable; /32 single address` + tests (BUG-005)
