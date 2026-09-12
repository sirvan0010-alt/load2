# MailLoadTester (load2) — plán práce

**Baseline:** `main` · full CI green without `--filter`  
**Source of truth:** this file + `VERIFICATION.md` + `BUGS-AUDIT.md`  
**FIXED only with:** commit SHA + CI run URL (see VERIFICATION.md)

---

## Status board

| Phase | Topic | Status |
|-------|--------|--------|
| **0** | CI, compile, quarantine lift | ✅ DONE |
| **A** | Execution model 1.1–1.6 / BUG-001,003,007,009 | ✅ **FIXED** — [VERIFICATION](VERIFICATION.md) |
| **B** | DeliveryLedger / AutoRestart / BUG-002 | ✅ **FIXED** — [VERIFICATION](VERIFICATION.md) |
| **C** | Proxy rotation BUG-004 | ⏳ NEXT |
| **D** | IPv4 /31 /32 BUG-005 | ⏳ |
| **E** | Repo integrity BUG-006 (cleanup patch-*.yml) | ⏳ parallel OK |
| **F** | Direct MX BUG-008 | ⏳ |
| **G** | Security SEC-AUDIT | ⏳ |
| **H** | Concurrency / TLS matrix | ⏳ |
| **I** | Plugin / release | ⏳ last |

---

## AI workflow (mandatory)

```
1. git pull main
2. Read PLAN.md + VERIFICATION.md + relevant BUGS-AUDIT section
3. One BUG / one phase per change set
4. dotnet build -c Release && dotnet test -c Release  (no weakened asserts, no new --filter)
5. FIXED only after green CI + entry in VERIFICATION.md
6. Do not start C–I until A+B FIXED (done)
```

### Never-do
- Disable tests to green CI  
- Drive-by refactors outside the active BUG  
- Mark FIXED without CI evidence  

---

## Immediate next commits (Phase C / E)

1. `chore(ci): remove obsolete one-shot patch-*.yml workflows` (E)
2. Audit `ProxyRotator` — blocked endpoint still sampled? (C / BUG-004)
3. Tests: ban skips proxy; all banned → clear error; no false exhaustion
4. Only then BUG-005 / 008

---

## Definition of done (MVP)

- [x] Phase A FIXED with CI
- [x] Phase B FIXED with CI
- [ ] Phase C–F CLOSED or DEFERRED with reason
- [ ] SEC-AUDIT critical items FIXED
- [x] Full `dotnet test` on Linux CI without filter
- [ ] README-MAINTENANCE points at PLAN + VERIFICATION
