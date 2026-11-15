# MailLoadTester 2.9.39 — build a použití

Verze aplikace je řízena souborem `VERSION`. Hodnota musí odpovídat `AppVersion.Current` a verzi uvedené v tomto dokumentu; CI jejich shodu automaticky kontroluje.

## Nejjednodušší spuštění ve Windows

Pro běžné používání **není potřeba instalovat .NET SDK ani Visual Studio**.

1. Otevři [Windows package workflow](https://github.com/sirvan0010-alt/load2/actions/workflows/windows-package.yml).
2. Vyber poslední úspěšný běh workflow na větvi `main`.
3. V části **Artifacts** stáhni `MailLoadTester-win-x64`.
4. Rozbal ZIP do libovolné složky.
5. Spusť `MailLoadTester.exe`. Soubor `START-HERE.txt` obsahuje stručný návod.

Balíček je self-contained pro Windows x64 a obsahuje .NET runtime. Není nutné ručně přesouvat DLL ani instalovat vývojové nástroje. Balíček je dostupný jako GitHub Actions artifact po dobu 30 dnů od běhu workflow. Při každé změně na `main` se automaticky vytvoří nový balíček.

## Vývojářský build

Vyžaduje .NET 8 SDK a Windows pro WinForms GUI.

### Self-contained EXE

V kořeni repozitáře spusť:

```bat
build.bat
```

Výstup: `publish\MailLoadTester.exe`. Self-contained balíček obsahuje .NET runtime, takže na cílovém PC není nutné instalovat .NET.

### Instalátor

```bat
build-installer.bat
```

Vyžaduje [Inno Setup](https://jrsoftware.org/isdl.php). Výstup: `installer\Output\Setup-MailLoadTester.exe`.

### Build a testy z příkazové řádky

```powershell
dotnet restore src/MailLoadTester.Core/MailLoadTester.Core.csproj
dotnet build src/MailLoadTester.Core/MailLoadTester.Core.csproj -c Release

dotnet test tests/MailLoadTester.Tests/MailLoadTester.Tests.csproj -c Release

dotnet restore src/MailLoadTester.Gui/MailLoadTester.Gui.csproj
dotnet build src/MailLoadTester.Gui/MailLoadTester.Gui.csproj -c Release
```

CI ověřuje Core a testy na Ubuntu a WinForms GUI na Windows. Samostatný Windows package workflow navíc vytváří self-contained ZIP pro koncové uživatele.

## Použití aplikace

GUI obsahuje osm záložek:

1. **SMTP Server** — host, port, TLS, autentizace, source IP, IP rotace, mTLS a timeouty.
2. **Zpráva** — From/To/CC/BCC, předmět, text/HTML, SMTPUTF8, přílohy, inline obrázky, EML šablona a hlavičky.
3. **Test** — počet zpráv, paralelismus, interval, retry, dávky, dry-run a Test Mode.
4. **Proxy** — SOCKS5 a seznam proxy serverů.
5. **Pokročilé** — Direct MX, pre-warming, adaptive concurrency, circuit breaker, lokální dashboard, AutoRestart a session log.
6. **Síť / integrace** — webhook, bandwidth limit a IP verze.
7. **Tempo a ochrana** — provider preset, jitter, burst, backoff, limity na příjemce, časové okno, warm-up, greylisting, RBL a pozorované SMTP odpovědi.
8. **SMTP log** — přehled SMTP událostí.

Po vyplnění nastavení klikni na **START**. Během běhu sleduj průběh, ETA, workery, stav pipeline a SMTP log.

## Bezpečnost testování

- Začni v režimu **Test Mode** nebo **Dry-run**.
- Reálné odesílání mimo Test Mode vyžaduje explicitní potvrzení přes `AuthorizationGate`.
- Testuj pouze SMTP servery, účty, domény a příjemce, k jejichž zatížení máš oprávnění.
- Nepoužívej veřejné poskytovatele jako zátěžový cíl bez jejich výslovného souhlasu.
- Neignoruj chyby certifikátů v produkčním prostředí.
- Neukládej hesla do sdílených profilů ani reportů.

## Výsledky a diagnostika

Po dokončení běhu můžeš použít tlačítko **Export report JSON**. Report obsahuje konfiguraci bez hesel, časy běhu, počty úspěchů/chyb, throughput, latence, histogram, retry/outcome statistiky, frontu a zdraví SMTP endpointů.

Report se ukládá pouze na výslovnou žádost uživatele. Aplikace automaticky neukládá historii SMTP konfigurací.

Volitelný HTTP dashboard běží lokálně na počítači a zobrazuje průběh, throughput, fázi, hloubku fronty a limit workerů. SMTP session log je samostatná funkce a může obsahovat citlivé provozní údaje; s jeho soubory zacházej jako s důvěrnými.

## Další dokumentace

- [Index dokumentace](docs/README.md)
- [Vývoj a architektura](docs/DEVELOPMENT.md)
- [Údržba a bezpečnostní invarianty](README-MAINTENANCE.md)
