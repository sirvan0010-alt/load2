# Nastavení GitHub Actions a ochrany větve main

Tento návod popisuje nastavení repozitáře `sirvan0010-alt/load2`. Workflow soubory už jsou v repozitáři; tato stránka se týká nastavení, která nelze uložit běžným commitem do zdrojového kódu.

## 1. Co už je nastavené v repozitáři

Větev `feature/mailbox-capacity-adaptive` obsahuje:

- CI: Core build, plnou sadu testů, Windows Forms GUI build a kontrolu odkazů v uživatelské dokumentaci;
- Windows package: self-contained Windows x64 ZIP, testy, SHA-256 a kontrolu obsahu archivu;
- CodeQL pro C# a GitHub Actions;
- Dependency Review pro změny závislostí v PR;
- workflow-hygiene kontrolu integrovanou do CI a Architecture Consistency;
- NuGet audit, který zastaví restore při závažných a kritických zranitelnostech;
- Dependabot weekly update konfiguraci pro NuGet a GitHub Actions.

Bezpečnostní workflow byla změněna tak, aby se spouštěla na pull requestech do libovolné cílové větve, ne pouze do `main`. PR #38 nyní míří do `feature/comprehensive-tooltips`, ale tyto kontroly se na něm přesto spouštějí.

## 2. Nastavení Actions permissions

1. Otevři repozitář [load2](https://github.com/sirvan0010-alt/load2).
2. Klikni na **Settings**.
3. V levém menu otevři **Actions → General**.

Doporučené nastavení:

### Actions permissions

Použij buď:
- **Allow select actions and reusable workflows** a povol pouze níže uvedené akce, nebo
- méně restriktivně **Allow actions created by GitHub** a **Allow actions by Marketplace verified creators**.

Aktuální workflow používají tyto akce:

- `actions/checkout`
- `actions/setup-dotnet`
- `actions/upload-artifact`
- `actions/dependency-review-action`
- `actions/github-script`
- `github/codeql-action`

Pokud zvolíš seznam povolených akcí, povol tyto organizace/repozitáře s wildcardem verzí, například `actions/checkout@*`. Nepovoluj bez důvodu všechny neověřené externí akce.

**Zatím nezapínej** volbu vyžadující, aby každá akce byla připnutá na plný 40znakový commit SHA. Workflow nyní používají explicitní major verze (`@v7`, `@v5`, `@v4`), nikoli plné SHA. Nejdříve by bylo nutné všechny reference připnout a udržovat jejich aktualizace.

### Workflow permissions

V části **Workflow permissions** nastav:

- **Read repository contents and packages permissions** jako výchozí oprávnění GITHUB_TOKEN.
- **Allow GitHub Actions to create and approve pull requests** nech vypnuté.

Jednotlivé workflow mají potřebná oprávnění uvedená přímo v YAML. Například CodeQL zapisuje bezpečnostní výsledky a Feature Architect může vytvářet issue; kvůli tomu není nutné dát všem workflow globální write přístup.

### Fork pull request workflows

U veřejného repozitáře ponech vyžadování schválení běhů od prvních přispěvatelů. Nepovoluj automaticky nedůvěryhodným forkům přístup k tajným údajům.

### Retention

V části **Artifact and log retention** můžeš ponechat 30 dní. Windows package workflow nastavuje artifact retention také na 30 dní. To je dostatečné pro stažení a ruční ověření vydaného balíčku bez dlouhodobého hromadění artefaktů.

Oficiální návod: [Managing GitHub Actions settings](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-github-actions-settings-for-a-repository).

## 3. Zapnutí bezpečnostních funkcí repozitáře

1. V **Settings** otevři **Code security and analysis** (v některých zobrazeních je položka pod **Security and quality**).
2. Zkontroluj a podle dostupnosti zapni:
   - **Dependency graph**;
   - **Dependabot alerts**;
   - **Dependabot security updates**;
   - **Secret scanning**;
   - **Push protection**;
   - **Code scanning**.

Dependency Review a CodeQL workflow už v repozitáři jsou. Dependency graph je ale předpokladem pro Dependabot alerts. Pokud některá funkce není v aktuálním plánu nebo nastavení účtu dostupná, GitHub ji v UI nabídne jako nedostupnou.

Oficiální návod: [Managing security and analysis settings](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-security-and-analysis-settings-for-your-repository).

## 4. Ochrana větve main – připravený import

V repozitáři je soubor [github-main-ruleset.json](github-main-ruleset.json). Je připraven jako ruleset pro `main` a obsahuje:

- zákaz smazání větve;
- zákaz force-push;
- povinný pull request;
- vyřešení všech konverzací v PR;
- povinné úspěšné CI, Windows package, CodeQL, Dependency Review a Architecture checks;
- požadavek, aby kontroly odpovídaly aktuálnímu HEAD commitu;
- nulový počet povinných schválení, aby se samostatný vlastník repozitáře nezablokoval tím, že nemá kdo schválit jeho vlastní PR.

Import:

1. Otevři **Settings → Rules → Rulesets**.
2. Klikni na **New ruleset → Import a ruleset**.
3. Vyber soubor `docs/github-main-ruleset.json` stažený z repozitáře.
4. Zkontroluj název, cílovou větev `main`, stav **Active** a seznam kontrol.
5. Klikni na **Create**.

Pravidlo cílí pouze na `main`. Aktuální PR #38 má jako cílovou větev `feature/comprehensive-tooltips`, takže jej tato ochrana main sama o sobě neblokuje. Při budoucím PR do `main` budou kontroly povinné.

GitHub může v nabídce status checks zobrazit názvy až poté, co se na cílové větvi objeví odpovídající běhy. Pokud se některý název v UI nenabídne, zadej jej přesně podle seznamu v JSON. Nevybírej neúspěšný `Feature Architect` push run; není součástí povinných PR kontrol.

Oficiální návod: [Creating rulesets for a repository](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/creating-rulesets-for-a-repository).

## 5. Které kontroly mají být povinné

| Check name v GitHubu | Co ověřuje |
|---|---|
| `build-test` | Core build, kompletní testovací sadu, kontrolu odkazů dokumentace a workflow hygiene |
| `build-gui` | Release build Windows Forms GUI |
| `package` | Windows ZIP, testy a SHA-256 balíčku |
| `CodeQL` | Výsledek code scanning |
| `Analyze (csharp)` | CodeQL analýzu C# |
| `Analyze (actions)` | CodeQL analýzu workflow |
| `Dependency Review` | Změny závislostí v PR |

| `Repository Architecture` | Architektonické invarianty repozitáře |

Tyto názvy byly ověřeny v úspěšném check-run výpisu aktuálního PR.

## 6. Jak poznat, že je vše aktivní

Na záložce **Actions** ověř, že pro aktuální PR proběhly:

1. CI;
2. Windows package;
3. CodeQL Advanced;
4. Dependency Review;
5. Actions Hygiene;
6. Architecture Consistency.

Na záložce **Security** sleduj Code scanning, Dependabot a Secret scanning alerts. Dependabot version updates podle přidaného `.github/dependabot.yml` kontroluje NuGet a GitHub Actions každý týden.

## 7. Co záměrně nepřidávat

- Žádné testy proti živým veřejným SMTP poskytovatelům. Automatické integrační testy mají používat lokální skriptovaný SMTP server.
- Žádný noční běh stejné sady jen kvůli počtu workflow. Aktuální kompletní testy běží na každém PR i v Windows package workflow. Samostatný nightly běh začne dávat smysl až po oddělení delších výkonových/regresních testů.
- Nevyžadovat jedno schválení PR, dokud je repozitář spravován jedním člověkem; mohlo by to zablokovat vlastní změny.
