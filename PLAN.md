# MailLoadTester (load2) — kompletní plán práce

**Baseline:** `main` @ `5b46ac6` · CI full suite **zelená bez `--filter`**  
**Pravidlo:** main = SOURCE OF TRUTH. Žádný FIXED bez `dotnet build` + `dotnet test` důkazu na CI.  
**Pro AI:** čti nejdřív tento soubor → `BUGS-AUDIT.md` → `README-MAINTENANCE.md`. Neměň scope mimo aktuální fázi.

---

## 0. Co už je hotové (neotvírat znovu bez regrese)

| Oblast | Stav | Důkaz |
|--------|------|--------|
| CI restore/build/test | ✅ | GitHub Actions, plná sada, bez karantény |
| Compile blockery (Dashboard, Models `\\`, pathPool, mxHosts) | ✅ | Core build 0 errors |
| Test quarantine lift | ✅ | 116 tests, 0 skipped (lokálně + CI #40) |
| RateLimiter concurrent + cancel kontrakt | ✅ | `_lastGranted`, `Task.Yield`, upravené testy |
| Bogus locale, PNG fallback, IdleClient NOOP kontrakt | ✅ | implementace + testy |
| AttachmentPlanner reject `>128` MB + BLOKOVÁNO | ✅ | Estimate + testy |

---

## 1. Fáze A — Execution model (bod 1 / BUG-001,003,007,009)

**Cíl:** MaxConcurrency = počet aktivních workerů; pacing až těsně před skutečný SEND; retry znovu přes SEND gate.

| Sub | Požadavek | Stav kódu (statika) | FIXED? |
|-----|-----------|---------------------|--------|
| **1.1** bounded worker pool | `Channel.CreateBounded` + `workerCount = MaxConcurrency` | ✅ v `SmtpTestRunner` | 🟡 kandidát — CI regression testy runneru musí explicitně assertovat bound |
| **1.2** first SEND immediate | první `AcquireSendSlotAsync` bez zbytečného wait | ✅ SmartPace (`_lastActualSendTicks`) | 🟡 |
| **1.3** retry pacing | každý retry znovu `AcquireSendSlotAsync` | ✅ slot uvnitř retry smyčky před `SendAsync` | 🟡 |
| **1.4** actual-SEND gate | pořadí: adaptive → pool → client → **AcquireSendSlot** → SendAsync | ✅ | 🟡 |
| **1.5** regression/cancellation | cancel neuvolní cizí slot; partial results | částečně testováno | 🟡 |
| **1.6** syntax/integrity | build main zelený | ✅ | ✅ build |

**Další práce A (priorita):**
1. Doplnit/ověřit testy: N zpráv, MaxConcurrency=K ⇒ max K současných workerů (semantický test, ne jen compile).
2. Test: první SEND latency ≪ interval; 2. SEND ≥ interval.
3. Test: transient fail → retry znovu čeká na SEND gate.
4. Až A1–A3 projedou na CI → v `BUGS-AUDIT.md` označit 1.1–1.6 / BUG-001/007/009 jako FIXED s odkazem na commit + run.

**Nesahat:** nové features, plugin, UI redesign.

---

## 2. Fáze B — Delivery correctness / AutoRestart (bod 2 / BUG-002)

**Cíl:** Accepted zpráva se při AutoRestart nikdy neodešle znovu; Failed lze claimnout znovu; agregované Sent = unique Accepted.

| Sub | Požadavek | Stav |
|-----|-----------|------|
| **2.1** message identity | index 1..MessageCount stabilní napříč restarty | ✅ ledger index |
| **2.2** in-memory ledger | Pending → InFlight → Accepted/Failed | ✅ `DeliveryLedger` |
| **2.3** restart skip Accepted | `TryClaim` false pro Accepted | ✅ |
| **2.4** aggregate counters | finální Sent/Failed z ledgeru | ✅ `RunAsync` sumace |
| **2.5** regression test | `AutoRestart_DoesNotResendAlreadyAcceptedLogicalMessage` | ✅ existuje |
| **2.6** cancel / InFlight recovery | InFlight po cancel nesmí viset navždy | 🟡 ověřit okraje |

**Další práce B:**
1. Audit okrajů: cancel během InFlight; dvojí `MarkFailed`; claim po Failed.
2. Test: cancel uprostřed AutoRestart → žádný hang, žádná duplicita Accepted.
3. Teprve pak BUG-002 = FIXED.

---

## 3. Fáze C — Proxy rotation (bod 3 / BUG-004)

- Eligibility blocked endpointů, exhaustion, ban window.
- Testy: všechny proxy banned → jasná chyba; rotace po ban.
- **Nezačínat**, dokud A+B nejsou FIXED s CI důkazem.

## 4. Fáze D — IPv4 rotation (bod 4 / BUG-005)

- RFC 3021 `/31` a `/32` semantika v `IpV4Rotator`.
- Testy edge prefixů.

## 5. Fáze E — Repo integrity (bod 5 / BUG-006)

- Žádné flatten řešení, žádné `.git` artefakty v tree.
- `MailLoadTester.sln` + src/tests layout stabilní.
- Uklidit mrtvé one-shot workflow (`patch-*.yml`), pokud už nejsou potřeba.

## 6. Fáze F — Direct MX hardening (bod 6 / BUG-008)

- Runner bezpečný i při bypass validation / mixed recipient domains.
- Explicitní chyba při DirectMx + více domén.

## 7. Fáze G — Security (bod 7 / SEC-AUDIT-001..004)

- AUTH redaction v session logu.
- Filesystem boundaries (přílohy, EML, session log path).
- Secret provenance (hesla ne v plain logu).
- `--unauthorized` + DryRun = žádný reálný network send.

## 8. Fáze H — Concurrency / network / TLS (bod 8)

- Combined state-machine stress (CONC-AUDIT).
- Socket / source-IP / proxy kombinace.
- TLS matrix (None, StartTls, ImplicitTls) + cert ignore flag.

## 9. Fáze I — Plugin architecture & automatizace (bod 9+)

- Až po stabilním core (A–H).
- Plugin boundary, CI matrix (Linux + Windows), případně release pipeline.
- **Nejdřív** zúžit dokumentační chaos (desítky AUDIT-FIXES-*) do jednoho living `PLAN.md` + `BUGS-AUDIT.md`.

---

## Pracovní postup pro jakoukoli AI (povinné)

```
1. git checkout main && git pull
2. Přečti PLAN.md + relevantní sekci BUGS-AUDIT.md
3. Jedna fáze / jeden BUG najednou — ne mixed PR
4. Změny: minimální diff, zachovej public API kde to jde
5. Lokálně: dotnet build -c Release && dotnet test -c Release
6. CI musí být zelené BEZ oslabení assertů a BEZ nového --filter
7. BUG status → FIXED jen s: commit SHA + CI run URL + co test pokrývá
8. main se nerozbíjí „wip“; experiment na fix/* branch
```

### Never-do
- Nevypínat testy kvůli zelené CI.
- Nerefactorovat nesouvisející soubory „za pochodu“.
- Neoznačovat FIXED bez důkazu.
- Negenovat velké přílohy před `EstimateRandomAttachments`.
- Neměnit `From` automaticky (RandomTestData kontrakt).

---

## Doporučené pořadí commitů (nejbližší sprint)

1. `test(runner): assert bounded concurrency under load` (1.1)
2. `test(pace): first send immediate + interval between actual SENDs` (1.2/1.4)
3. `test(pace): retry re-enters AcquireSendSlotAsync` (1.3)
4. `docs(bugs): mark 1.x FIXED when CI green`
5. `test(ledger): cancel during InFlight + AutoRestart edges` (2.6)
6. `docs(bugs): mark BUG-002 FIXED`
7. `chore(ci): remove obsolete patch-*.yml workflows` (E)
8. Teprve BUG-004 / 005 / 008 …

---

## Definice hotovo pro celý projekt (MVP stabilita)

- [ ] Body 1–2 FIXED s CI
- [ ] Body 3–6 CLOSED nebo explicitně DEFERRED s důvodem
- [ ] SEC-AUDIT checklist projeden, kritické položky FIXED
- [ ] Plný `dotnet test` na Linux CI bez filtru
- [ ] `PLAN.md` + `BUGS-AUDIT.md` synchronní se skutečností
- [ ] README-MAINTENANCE aktuální pro další AI
