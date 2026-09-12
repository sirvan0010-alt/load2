# Požadavky na GUI – MailLoadTester

## 1. Cíl
Moderní, čisté a profesionální GUI pro SMTP/load testing. Design má být inspirován minimalistickou estetikou Tesla UI: velké čisté plochy, minimum vizuálního šumu, výrazné stavové informace a rychlá orientace. Nejde o kopírování proprietárního rozhraní Tesly.

## 2. Dark Mode
- Dark Mode je výchozí motiv.
- Preferovat tmavé antracitové/černé plochy s vysokým kontrastem textu.
- Všechny komponenty musí mít konzistentní dark-theme variantu.
- Přidat přepínač Dark / Light Mode.
- Volba motivu se má zachovat mezi spuštěními.
- Nepoužívat hardcoded barvy roztroušené po jednotlivých formulářích; barvy a rozměry mají být centralizované v theme vrstvě.

## 3. Vizuální styl
- Minimalistický moderní desktop UI.
- Čistá typografie a dostatek prostoru mezi prvky.
- Zaoblené panely/karty pouze tam, kde zlepšují hierarchii.
- Jemné rámečky a separátory místo těžkých gradientů.
- Výrazný primární action button.
- Stavové barvy musí být použity konzistentně pro READY, CONNECTING, SENDING, SUCCESS, FAILED, CANCELLED.
- Žádné zbytečné animace; animace pouze pro zpětnou vazbu a stav.

## 4. Hlavní obrazovka
Navrhované rozložení:

```text
┌──────────────────────────────────────────────────────────────────────┐
│ MailLoadTester                                      ● READY   ☾      │
├──────────────────────────────────────────────────────────────────────┤
│ SMTP CONFIGURATION                                                   │
│ Host [ smtp.example.com ]   Port [ 587 ]   TLS [ STARTTLS ▼ ]       │
│ User [ ................ ]   Password [ ................ ]            │
│                                                                      │
│ TEST CONFIGURATION                                                   │
│ Target [ recipient@example.com ]                                    │
│ Messages [ 100 ]     Concurrency [ 4 ]     Interval [ 500 ] ms      │
│                                                                      │
│                 [ TEST SMTP ]        [ SEND ]                        │
│                                                                      │
│ PROGRESS                                                             │
│ ████████████████████████████░░░░░░░░░░  72%                         │
│                                                                      │
│ Sent: 72       Failed: 1       Cancelled: 0       Time: 00:00:38    │
│                                                                      │
│ SMTP SESSION                                                         │
│ ● Connected    TLS 1.3    Session reuse: ON    Rate: 2.1 msg/s      │
└──────────────────────────────────────────────────────────────────────┘
```

## 5. Ovládání
- `TEST SMTP` provede bezpečný test připojení a autentizace podle konfigurace.
- `SEND` spustí vlastní test podle nastavených parametrů a uživatelem zadaného konkrétního cíle/příjemce.
- Cíl není automaticky nahrazován interním pevným seznamem; je součástí testovací konfigurace.
- Během běhu se `SEND` nesmí spustit podruhé.
- Přidat jednoznačné `STOP/CANCEL` ovládání s podporou CancellationToken.
- Po dokončení se GUI vrátí do READY stavu.
- Chyby zobrazovat přímo u relevantní části konfigurace a zároveň do log panelu.

## 6. Progress a telemetry
Zobrazovat živě minimálně:
- procenta dokončení,
- Sent,
- Failed,
- Cancelled,
- elapsed time,
- aktuální rate,
- aktuální concurrency,
- stav SMTP session,
- TLS stav,
- případně počet retry.

GUI nesmí blokovat UI thread. Aktualizace UI musí být bezpečné vůči souběžnému běhu testu.

## 7. SMTP konfigurace
- Host, port, TLS režim, autentizace a další parametry musí být jasně oddělené.
- Hesla nikdy nezobrazovat v logu.
- Citlivé údaje nesmí být hardcoded ve zdrojovém kódu.
- Preferovat bezpečné načítání konfigurace z existující konfigurační vrstvy/environment variables podle architektury projektu.
- Direct MX režim musí být vizuálně odlišen od běžného SMTP serveru.

## 8. Pokročilé nastavení
Pokročilé parametry skrýt do samostatného panelu, aby základní obrazovka zůstala jednoduchá:
- adaptive concurrency,
- rate limiting,
- retry policy,
- circuit breaker,
- proxy nastavení,
- Direct MX,
- payload/plugin nastavení.

## 9. Bezpečnost a autorizace
- Povinný parametr CLI `--unauthorized` zůstává zachován podle projektových požadavků.
- Testovací funkce musí být explicitně řízené a zrušitelné.
- `--unauthorized` je explicitní potvrzení uživatele pro živé odesílání; není to náhrada za technické řízení concurrency/pacing/cancellation.
- GUI nesmí obcházet existující AuthorizationGate, pacing, concurrency, cancellation nebo další síťové ochrany.
- GUI nemá samo vytvářet mechanismus pro obcházení limitů poskytovatele nebo skrývání provozu.

## 10. Architektura UI
GUI má zůstat oddělené od síťové vrstvy a generování MIME zpráv.

Preferované vrstvy:
- UI / presentation
- application/test orchestration
- SMTP/network layer
- payload/plugin layer
- configuration/security

GUI pouze orchestrace a prezentace stavu; nemá přímo implementovat SMTP protokol.

## 11. Theme systém
Centralizovat minimálně:
- background/surface colors,
- text colors,
- accent color,
- success/warning/error colors,
- border radius,
- spacing,
- typography,
- button styles,
- progress-bar style.

Theme musí být možné později rozšířit bez přepisování všech formulářů.

## 12. UX zásady
- Nejdůležitější akce musí být dostupné bez otevírání dialogů.
- Chybové stavy musí být srozumitelné i bez čtení logu.
- Konfigurace nesmí být zbytečně komplikovaná.
- Výchozí hodnoty mají být bezpečné a praktické.
- Pokročilé funkce mají být dostupné, ale nemají zahlcovat hlavní obrazovku.

## 13. Auditovatelnost
Při implementaci UI se nesmí měnit síťová logika jen kvůli vzhledu. Každá změna chování musí být samostatně auditovatelná a testovatelná.

## 14. Akceptační kritéria
- Dark Mode funguje jako výchozí režim.
- Light/Dark přepnutí nemění funkčnost testu.
- GUI během testu nezamrzá.
- START/STOP je bezpečný při souběžných operacích.
- Progress a counters odpovídají skutečnému stavu runneru.
- Citlivá data se neobjeví v UI logu ani výjimkách.
- Vizuální styl je konzistentní napříč celou aplikací.
- GUI zachovává aktuální cílový model a nepřepisuje uživatelem zadaného příjemce.
- GUI neobchází AuthorizationGate, pacing, concurrency nebo cancellation.
