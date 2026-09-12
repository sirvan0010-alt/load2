# Deep Audit Round 4 — stav rozpracovanosti

Podle plánu z předchozí zprávy (10 bodů). Co je hotovo v tomto kole, co ne —
u nedokončených bodů explicitně NEOVĚŘENO, ne že "je to v pořádku".

## Hotovo

### 1. SmtpConnectionPool.cs — kompletní průchod
Prošel jsem RentAsync (idle-reconnect, health-check, nová connection),
Return/Discard (idempotence, double-return), PreWarmAsync, DisposeAsync.
**Ověřeno, žádný nový bug:** obava z "DisposeAsync race s aktivně používaným
leased klientem" se nepotvrdila — `SmtpTestRunner` vždy čeká na
`Task.WhenAll(tasks)` (které per definici nevrátí řízení, dokud nejsou
VŠECHNY tasky dokončené, ať už úspěchem, chybou i zrušením) předtím, než
`finally` zavolá `pool.DisposeAsync()`. Nebyl nalezen scénář, kde by DisposeAsync
běžel souběžně s worker vláknem aktivně používajícím leased klienta.
**Oprava:** `catch{}` na řádku 116 (idle reconnect failure) teď loguje
příčinu do session logu místo tichého zahození.

### 2. Všech 12 catch{} — klasifikováno
- `SmtpConnectionPool.cs:116` → opraveno (log místo ticha).
- `DashboardServer.cs:47,77` → zdokumentováno komentářem (shutdown /
  best-effort), beze změny chování.
- `SmtpSessionLogger.cs:52` → **skutečná oprava.** Buffer se mazal PŘED
  zápisem na disk, takže při selhání zápisu (plný disk, zamčený soubor)
  se diagnostický obsah nenávratně ztratil beze stopy. Teď se při selhání
  vrací zpět do bufferu (retry při dalším tiku timeru), s bezpečnostním
  stropem na velikost bufferu (4M znaků) proti neomezenému růstu při
  trvalé poruše disku.
- `MxResolver.cs:126`, `Models.cs:156`, `AttachmentPlanner.cs:203,210`,
  `NetworkAdapterInfo.cs:128,144,193`, `SmtpTestRunner.cs:626`,
  `WebhookNotifier.cs:38` → ověřeno, žádná oprava potřeba (buď už
  zdokumentované "best effort", nebo prokazatelně bezpečné díky
  `Task.WhenAll` sémantice, nebo čistě kosmetické UI info bez dopadu na
  korektnost testu).

### 3. Path security — pokračování auditu
- `ProfileStore.cs` chrání Save/Load přes `Validation.ContainsPathTraversal()`
  a `PathSecurity.EnsureNoReparsePoints()`.
- `EmlTemplateParser.cs` a `AttachmentPlanner.cs` byly již napojeny na
  `PathSecurity`.
- `ClientCertificateHelper.cs` nyní chrání cestu certifikátu přes
  `PathSecurity` a zachovává `X509KeyStorageFlags.EphemeralKeySet`.
- `SmtpSessionLogger.cs` nyní chrání cestu session logu přes `PathSecurity`.
- `MailPayloadPluginLoader.cs` nyní odmítá reparse point na `plugins`
  adresáři i na jednotlivých `.dll` souborech před `AssemblyLoadContext`.
- Přidán `MailPayloadPluginLoaderSecurityTests.cs` pro Windows symlink
  adresář/soubor (s bezpečným skipem, pokud prostředí symlink nepovolí).

### 4. AUTH secret redaction — statické ověření zapojení
`SmtpConnectionPool` vytváří `SmtpClient(IProtocolLogger)`. Oficiální zdroj
MailKit 4.x potvrzuje, že tento konstruktor nastavuje na předaném loggeru
`AuthenticationSecretDetector` přímo z interního `SmtpAuthenticationSecretDetector`.
`SessionProtocolLogger` tuto property předává do `ProtocolLogRedaction`.
Unit testy zároveň potvrzují maskování detekovaného secret range.
**End-to-end fake-SMTP test zatím není hotový**, takže runtime PASS se stále
neprohlašuje.

### 5. Phase 4 — composition audit concurrency/state machines
Proveden průchod `CircuitBreaker`, `RateLimiter`, `SmartPaceController`,
`AdaptiveConcurrencyLimiter`, `DeliveryLedger` a `SmtpConnectionPool`.
Nebyl nalezen nový source-confirmed primitive race mimo již opravený BUG-009.
Aktuální `SmtpTestRunner` drží pořadí:
`WaitBeforeSend → AdaptiveConcurrency → SMTP pool → AcquireSendSlot → SendAsync`.
Retry znovu vstupuje do `AcquireSendSlot`, `SendPaceLease` drží exclusive gate
přes skutečný `SendAsync`, a pool Return/Discard jsou idempotentní vůči double-return.
Stále chybí kombinovaný runtime stress/cancellation test celé sestavy.

### 6. CI po posledních změnách
Na `main` commit `bc77fe5f4d7c37b702a0c7c34a984861729863c9` proběhl:
- CI run 143 — **success**.
- CodeQL Advanced run 28 — **success**.

## NEOVĚŘENO

- **Bod 3 — AUTH secret redaction end-to-end test**: staticky potvrzené zapojení,
  ale fake SMTP integrační test stále chybí.
- **Bod 5 — úplný audit všech UNC/junction/symlink hranic**: známé produkční
  file-path consumers jsou nyní pokryty, ale systematická enumerace všech
  file-producing/reading features ještě není uzavřena.
- **Bod 7 — kombinovaný runtime stress/cancellation test** celé limiter/pool
  sestavy.
- **Bod 8 — systematický cancellation audit všech awaitů v Core**.
- **Bod 9/10 — lokální `dotnet test` / `dotnet build -c Release`** není zde
  spuštěn mimo GitHub Actions; aktuální CI/CodeQL jsou zelené.

## Další krok
1. Doplnit fake-SMTP AUTH redaction integration test.
2. Dokončit systematickou enumeraci file-path consumerů a konfigurace/secrets.
3. Doplnit kombinovaný cancellation/stress test limiter → pool → SEND.
4. Pak provést release-gate review a aktualizovat finální audit stav.

## Metoda ověřování
Čtení zdrojového kódu + GitHub CI evidence; bez lokálního .NET SDK. U
MailKit AUTH wiring byl navíc ověřen upstream zdroj MailKit 4.x.