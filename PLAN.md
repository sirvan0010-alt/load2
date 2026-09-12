# MailLoadTester (load2) — kompletní plán práce

**Baseline:** `main` · CI full suite **zelená bez `--filter`**  
**Pravidlo:** main = SOURCE OF TRUTH. Žádný FIXED bez `dotnet build` + `dotnet test` důkazu na CI.  
**Pro AI:** čti nejdřív tento soubor → `BUGS-AUDIT.md` → `README-MAINTENANCE.md`. Neměň scope mimo aktuální fázi.

---

## 0. Co už je hotové (neotvírat znovu bez regrese)

| Oblast | Stav | Důkaz |
|--------|------|--------|
| CI restore/build/test | ✅ | GitHub Actions, plná sada, bez karantény |
| Compile blockery | ✅ | Core build 0 errors |
| Test quarantine lift | ✅ | CI #40 |
| RateLimiter / Bogus / Estimate / IdleClient | ✅ | implementace + testy |

---

## 1. Fáze A — Execution model (bod 1 / BUG-001,003,007,009)

**Cíl:** MaxConcurrency = počet aktivních workerů; pacing až těsně před skutečný SEND; retry znovu přes SEND gate.

| Sub | Požadavek | Test / důkaz | FIXED? |
|-----|-----------|--------------|--------|
| **1.1** bounded worker pool | MaxConcurrentData ≤ MaxConcurrency | `BoundedConcurrency_DoesNotExceedConfiguredWorkerCount` · commit `20a3f19` · [CI #43](https://github.com/sirvan0010-alt/load2/actions/runs/34663469078) | 🟡 kandidát (CI OK) |
| **1.2** first SEND immediate | 1. slot ≪ interval | `FirstSend_IsImmediate_SecondRespectsInterval` · `76ac896` · [CI #42](https://github.com/sirvan0010-alt/load2/actions/runs/34663325465) | 🟡 kandidát (CI OK) |
| **1.3** retry → SEND path | 451 → 2. DATA attempt | `TransientRetry_IssuesSecondDataAttempt_ThroughSendPath` · `09f31cf` · [CI #44](https://github.com/sirvan0010-alt/load2/actions/runs/34663883453) | 🟡 kandidát (CI OK) |
| **1.4** actual-SEND gate | interval mezi SEND | `FirstSend_…` + `GlobalSpacing_WithConcurrency_RespectsInterval` · CI #42 | 🟡 kandidát (CI OK) |
| **1.5** cancellation | partial results | `Cancellation_ReturnsPartialResults` + SmartPace cancel tests | 🟡 existuje; zkontrolovat okraje |
| **1.6** syntax/integrity | build main | plný CI zelený | ✅ |

**Další práce A:**
1. Doplnit / zpřísnit 1.5 (cancel během retry, cancel během AutoRestart) pokud chybí.
2. Až 1.1–1.5 mají CI důkaz → v `BUGS-AUDIT.md` označit BUG-001/007/009 FIXED (SHA + run URL).
3. **Nezačínat** C–I před uzavřením A a B.

---

## 2. Fáze B — Delivery correctness / AutoRestart (BUG-002)

| Sub | Stav |
|-----|------|
| 2.1–2.5 ledger + regression | ✅ kód + `AutoRestart_DoesNotResendAlreadyAcceptedLogicalMessage` |
| 2.6 cancel / InFlight recovery | 🟡 ověřit |

---

## 3–9. Fáze C–I

Proxy (004) → IPv4 (005) → repo integrity (006) → Direct MX (008) → Security → Concurrency/TLS → Plugin.  
Podrobnosti beze změny oproti předchozí verzi PLAN.md — **nezačínat dřív než A+B FIXED**.

---

## Pracovní postup pro AI

```
1. git pull main
2. PLAN.md + BUGS-AUDIT.md
3. Jedna fáze / jeden BUG
4. dotnet build + test Release, bez oslabení assertů
5. FIXED jen s commit SHA + CI run URL
```

### Never-do
- Nevypínat testy kvůli zelené CI  
- Nerefactorovat nesouvisející soubory  
- Neoznačovat FIXED bez důkazu  

---

## Sprint stav (2026-09-12)

- [x] 1.1 test + CI #43  
- [x] 1.2/1.4 test + CI #42  
- [x] 1.3 test + CI #44  
- [ ] 1.5 okraje  
- [ ] docs: BUGS-AUDIT FIXED pro bod 1  
- [ ] Phase B 2.6  
- [ ] chore: smazat mrtvé `patch-*.yml`  
