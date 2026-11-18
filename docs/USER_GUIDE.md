# Uživatelská příručka – MailLoadTester

Tento návod popisuje běžné ovládání Windows Forms aplikace. Přesné chování vždy určuje aktuální verze programu; historické audity nemusí odpovídat poslednímu vydání.

## 1. Instalace a první spuštění

1. Otevři [Windows package workflow](https://github.com/sirvan0010-alt/load2/actions/workflows/windows-package.yml).
2. Vyber poslední úspěšný běh na větvi `main` a v části **Artifacts** stáhni `MailLoadTester-win-x64`.
3. Rozbal ZIP do samostatné složky.
4. Před spuštěním můžeš ověřit SHA-256 EXE podle souboru `SHA256SUMS.txt`.
5. Spusť `MailLoadTester.exe`. Balíček je self-contained pro Windows x64; samostatná instalace .NET Runtime není nutná.

Pro první seznámení použij **GETTING STARTED**, případně začni v režimu Dry-run. Program má více záložek; nápověda jednotlivých polí je dostupná také přes tooltip po najetí myší.

## 2. Bezpečný první test

Doporučený postup:

1. Na záložce **SMTP Server** vyplň SMTP host, port a odpovídající zabezpečení.
2. Vyplň účet pouze tehdy, pokud server vyžaduje autentizaci.
3. Na záložce **Zpráva** nastav odesílatele a jednoho testovacího příjemce, kterého smíš použít.
4. Na záložce **Test** nastav 1–5 zpráv, paralelismus 1 a interval alespoň 1 000 ms.
5. Nejprve zapni **Dry-run (bez reálného SMTP)** a ověř konfiguraci.
6. Vypni Dry-run pouze tehdy, když chceš skutečně odesílat. Před reálným odesíláním zkontroluj bezpečnostní banner a potvrzovací bránu autorizace.
7. Klikni na **Otestovat připojení**. Tato akce ověří SMTP spojení, ale sama neposílá zprávu.
8. Teprve potom spusť test tlačítkem **START**.

**Test Mode není Dry-run.** Test Mode omezuje povolené cílové domény; zprávy se při něm stále mohou skutečně odesílat. Dry-run simuluje běh bez SMTP komunikace.

## 3. SMTP Server

### Host, port a TLS

Běžné kombinace:

| Port | Typické použití | Zabezpečení v aplikaci |
|---|---|---|
| 25 | Přímá komunikace mezi poštovními servery / MX | obvykle None nebo STARTTLS podle serveru |
| 587 | Autentizované odesílání (submission) | StartTls |
| 465 | Autentizované odesílání s TLS od začátku | ImplicitTls |

Port a režim TLS musí odpovídat dokumentaci konkrétního poskytovatele. Port 25 může být z běžných sítí blokovaný. Volba **Ignorovat chyby certifikátu** je pouze diagnostická; pro běžné použití ji nech vypnutou.

### Autentizace

- **Auto** nechá MailKit vybrat kompatibilní metodu podle serveru.
- Pro Gmail použij SMTP server `smtp.gmail.com`, port **587 + STARTTLS** (nebo 465 + Implicit TLS), jako uživatelské jméno celou Gmail adresu a do pole **Heslo / Heslo aplikace** vlož **Heslo aplikace Google**. Běžné heslo Google sem nezadávej.
- Heslo aplikace vytvoříš na [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords). Google vyžaduje zapnuté dvoufázové ověření. Možnost nemusí být dostupná u účtů používajících pouze bezpečnostní klíče, u účtů s Advanced Protection nebo u pracovních/školních účtů, kde ji správce nepovolil.
- Heslo aplikace je samostatný přístupový údaj. Po změně běžného hesla Google může být odvoláno a bude nutné vytvořit nové.
- Při ruční SMTP metodě OAuth2 patří do pole Heslo access token, nikoli refresh token.
- Client certificate (.pfx) je určen pro mTLS, pokud jej server vyžaduje.
- Hesla a tokeny nevkládej do screenshotů, sdílených profilů ani veřejných issue.

### Zapamatování SMTP hesla

V kartě **SMTP účet a certifikáty** je volba **Zapamatovat heslo na tomto počítači**. Je standardně vypnutá.

- Zaškrtni ji, pokud chceš, aby aplikace po úspěšném testu připojení, jednorázovém odeslání nebo úspěšném běhu testu uložila SMTP host, port, uživatelské jméno, heslo, TLS a autentizační metodu.
- Údaje se ukládají do `%LOCALAPPDATA%\\MailLoadTester\\smtp-credential.dat` zašifrované Windows DPAPI v rozsahu aktuálního Windows uživatele.
- Při dalším spuštění se údaje automaticky načtou a volba zůstane zaškrtnutá.
- Odškrtnutím volby se uložený soubor smaže. Jiný Windows účet jej nemůže dešifrovat.
- Profily JSON nadále hesla neukládají. Tato funkce je samostatná a výslovně volitelná.

Pokud se přihlašuješ pomocí OAuth2 access tokenu v poli Heslo, nezapínej ukládání, pokud nechceš uložit i tento token.

### Přizpůsobení rozhraní

Pole a nápovědy se přizpůsobují šířce dostupného prostoru a dlouhé popisky se zalamují. Na kartě **SMTP Server** se při užším okně tři skupiny nastavení přeskládají pod sebe a stránka se zpřístupní svislým posuvníkem. Klasické SMTP přihlášení je hlavní a výchozí cesta; Google OAuth/Gmail API je skryté v rozšířených volitelných funkcích. Rozhraní používá škálování DPI pro monitory s různou hustotou pixelů.

### Volitelné přihlášení Gmail přes Google

MailLoadTester nabízí samostatné přihlášení Google a odeslání jedné zprávy přes Gmail API. Desktopová aplikace používá veřejné Client ID a Authorization Code + PKCE; ruční zadání Client Secret, hesla Gmailu ani import JSON souboru není potřeba. Obnovovací token Google účtu se ukládá zašifrovaně pomocí Windows DPAPI pro aktuální účet Windows.

1. V Google Cloud projektu musí být povolené **Gmail API**: [Google Cloud – Gmail API](https://console.cloud.google.com/apis/library/gmail.googleapis.com).
2. V režimu OAuth **Testing** musí být tvůj Google účet uvedený v seznamu testovacích uživatelů (Audience → Test users).
3. V MailLoadTester na kartě **SMTP Server** klikni na **Přihlásit přes Google**.
4. V otevřeném systémovém prohlížeči vyber Gmail účet a potvrď oprávnění k odesílání e-mailů.
5. Po návratu do aplikace uvidíš připojenou e-mailovou adresu. Obnovovací token se ukládá zašifrovaně pomocí Windows DPAPI pro aktuální účet Windows.
6. Příjemce nastav přímo ve třetím sloupci na kartě **SMTP Server**. Předmět, tělo a případné běžné přílohy nastav na kartě **E-mail (Od / obsah)**. Odesílatel musí odpovídat přihlášenému Gmail účtu.
7. Klikni na **Odeslat 1 zprávu přes Gmail API** a zkontroluj potvrzovací dialog.

Tato akce odešle právě jednu zprávu přes Gmail API. Nejde o SMTP spojení ani zátěžový test a výsledek se nezapočítává do SMTP metrik. Běžné přílohy ze seznamu Přílohy se připojí; inline obrázky, EML šablona a náhodné generování se v tomto samostatném API odeslání nepoužívají. Aplikace nikdy automaticky nepřepne na SMTP, pokud Gmail API selže.

Při odpojení aplikace požádá Google o odvolání obnovovacího tokenu a odstraní jeho místní zašifrovanou kopii.

**Pozor na režim Testing:** přihlásit se mohou pouze přidaní testovací uživatelé a Google omezuje životnost refresh tokenů. Pro veřejné vydání je nutné nastavit produkční OAuth projekt a dokončit požadované ověření citlivého oprávnění `gmail.send`.

### Source IP a Direct MX

Source IP určuje lokální adresu, ze které počítač navazuje spojení. IPv4/IPv6 rotace a proxy jsou pokročilé síťové možnosti; použij je jen v rámci oprávněného testu a nikdy k obcházení limitů poskytovatele.

**Direct MX delivery** posílá přímo na MX cílové domény místo použití SMTP relay. Vyžaduje funkční DNS, povolený odchozí port 25 a souhlas s testováním cílového serveru. Není to automaticky vhodná volba pro veřejné domény.

## 4. Účty SMTP (multi-account)

Záložka **Účty SMTP** umožňuje přidat více SMTP účtů. Každý záznam obsahuje ID, host, port, TLS, volbu autentizace, uživatelské jméno, heslo a stav Enabled. Pomocí tlačítek **Přidat**, **Upravit**, **Odebrat** a **Zap/Vyp** spravuješ seznam.

Důležité provozní pravidlo: multi-account režim se aktivuje až při **dvou nebo více povolených účtech**. Při nule nebo jednom účtu se používá klasická konfigurace na záložce SMTP Server. Jeden řádek v Účty SMTP tedy sám o sobě nenahrazuje hlavní SMTP konfiguraci.

Při více účtech má každý účet vlastní SMTP connection pool. Výběr preferuje zdravé endpointy, následně degradované; quarantined endpointy se nepoužívají. V rámci stejné zdravotní kategorie probíhá round-robin výběr. Pokud je zapnutý Pre-warm, program připraví spojení napříč účty.

Používej pouze účty a SMTP servery, jejichž zatěžování je výslovně povolené. Více účtů není prostředek k obcházení limitů nebo reputačních pravidel poskytovatele.

Při běžném uložení profilu se hesla všech SMTP účtů odstraní. Po načtení profilu je proto nutné hesla znovu doplnit. Do běhu se přenášejí pouze povolené účty; vypnuté položky se do konfigurace běhu ani profilu nezahrnují.

## 5. Zpráva

Na záložce **Zpráva** nastav:

- From, CC a BCC; příjemce To je společný a nastavuje se ve třetím sloupci karty SMTP Server;
- předmět, text nebo HTML tělo;
- SMTPUTF8 pro Unicode adresy, pokud jej server podporuje;
- přílohy, inline obrázky a vlastní hlavičky;
- volitelnou EML šablonu.

Inline obrázky lze v HTML odkazovat pomocí `{{cid:nazev_souboru_bez_pripony}}`. Náhodné přílohy mají bezpečnostní předvýpočet spotřeby RAM. Hodnota velikosti 0 znamená Auto; ruční nebezpečná kombinace se odmítne před velkou alokací.

## 6. Test – počet, paralelismus a interval

- **Počet zpráv**: požadovaný počet logických zpráv.
- **Paralelismus**: maximální počet souběžných workerů (1–20). Není to počet zpráv za sekundu.
- **Interval (ms)**: minimální globální rozestup mezi začátky skutečných SMTP SEND operací, nikoli interval každého workeru zvlášť.
- **Retry**: počet opakování při dočasné chybě podle klasifikace a limitů retry.

Při intervalu 0 ms není nastaven pevný časový strop. Skutečnou rychlost pak omezuje souběžnost, příprava MIME zprávy, síť a odezva SMTP serveru. Při kladném intervalu je orientační strop `60 000 / interval_ms` SEND za minutu. Například 1 000 ms znamená nejvýše přibližně 60 nových SEND za minutu.

### Capacity Run – bezpečnostní limity

Capacity Run je ve výchozím stavu vypnutý. Po zapnutí lze nastavit:

- **Capacity: max. zpráv** – horní limit logických zpráv;
- **Capacity: celkový MIME limit (MiB)** – součet serializovaných MIME bajtů pro všechny SEND pokusy;
- **Capacity: max. MIME zpráva (MiB)** – strop jedné zprávy;
- **Capacity: max. zpráv/min** – globální rychlostní strop včetně retry.

MIME velikost se měří až po sestavení zprávy a pluginech, těsně před SEND. Opakovaný SEND pokus se započítá znovu, i když skončí timeoutem nebo odmítnutím. Je to konzervativní účetnictví. MIME bajty nejsou totéž co skutečné síťové bajty ani velikost uložená ve schránce.

Pokud SMTP server po autentizaci oznámí `SIZE`, aplikace použije přísnější kladný limit z konfigurace a serveru. SMTP SIZE je limit jedné zprávy, nikoli kvóta schránky.

Tlačítko **Odhad MIME objemu** poskytuje předběžný odhad. Rozhodující je runtime měření skutečně připravené MIME zprávy.

### Úvodní validační zpráva

Volba **Před testem odeslat 1 malou validační zprávu** je standardně vypnutá. Pokud ji zapneš, aplikace po START odešle jednu skutečnou malou zprávu prvnímu příjemci ještě před spuštěním workerů. Započítá se do počtu zpráv, Capacity limitů a statistik. Při chybě se zátěžový běh nespustí a validační zpráva se automaticky neopakuje.

Zapínej ji pouze tehdy, když první příjemce je výslovně autorizovaná testovací schránka.

### Dávkový režim

Dávkový režim rozdělí zprávy do skupin a vloží pauzu mezi dávkami. Je vhodný pro simulaci postupného zatěžování; nenahrazuje globální interval ani Capacity limity.

## 7. Tempo a ochrana

Záložka **Tempo a ochrana** obsahuje předvolby poskytovatele a další limity:

- **Provider preset** nastaví výchozí kombinaci tempa a ochrany; jednotlivé hodnoty můžeš upravit.
- **Jitter** přidává náhodnou odchylku intervalu. Neobchází antispamové systémy.
- **Burst + pause** vloží pauzu po zadaném počtu zpráv.
- **Progressive backoff** může prodlužovat interval podle výsledků.
- **Limit na příjemce** omezuje počet zpráv pro jednu adresu v časovém okně.
- **Omezit na denní dobu** povolí odesílání jen v nastaveném časovém okně.
- **Warm-up plán** postupně mění tempo podle počtu úspěšných zpráv.
- **Detekovat greylist** odkládá opakování dočasně odmítnutých zpráv.
- **RBL kontrola** je diagnostická kontrola zdrojové IP.

Tyto volby slouží k řízenému a autorizovanému testování. Nepoužívej je k obcházení pravidel, reputačních kontrol nebo limitů veřejných poskytovatelů.

## 8. Pokročilé

- **Adaptive concurrency** začíná na nastaveném maximu, při dočasném tlaku serveru nebo růstu latence souběžnost snižuje a po zdravých oknech ji může postupně obnovit nejvýše na nastavené maximum. Permanentní 5xx politika sama o sobě neznamená přetížení.
- **Circuit breaker** zastaví další pokusy po dosažení nastaveného prahu selhání.
- **HTTP Dashboard** spustí lokální dashboard na nastaveném portu; nevystavuj jej do veřejné sítě.
- **Auto-restart** může opakovat nevyřešené části testu. Sleduj počet pokusů a výsledky; již přijaté logické zprávy se díky delivery ledgeru znovu neposílají.
- **SMTP session log** je ve výchozím stavu vypnutý. Při zapnutí může zaznamenat adresy, hlavičky a obsah. Aktivní soubor má limit 8 MiB a uchovávají se nejvýše tři zálohy.

## Tor / SOCKS5 – srozumitelné vysvětlení

Tor funguje jako prostředník mezi tvým počítačem a SMTP serverem. Pokud se e-mail posílá přes Tor, SMTP server vidí síťovou adresu Tor výstupního uzlu, nikoli přímo veřejnou IP adresu tvého domácího připojení.

Pokud Tor přestane fungovat, spojení přes SOCKS5 má selhat. MailLoadTester nesmí přepnout na přímé připojení jako náhradní řešení. V případě neúspěšného ověření Toru aplikace aktivní test zastaví. Kontrola stavu se opakuje každých 5 sekund; mezi kontrolami se ale mohou projevit síťové chyby dříve. Samotná zelená ikona není zárukou anonymity každého jednotlivého síťového paketu.

**Důležité:** Tor skryje síťovou IP před cílovým serverem, ale neskryje účet, kterým ses přihlásil. Pokud odešleš zprávu ze svého osobního Gmailu, Google zná tvůj účet a příjemce zpravidla uvidí skutečnou adresu Gmailu. Náhodné zobrazované jméno tuto adresu nemění.

Po dokončení běhu se na samostatné kartě **Přehled zpráv** zobrazí jednotlivé zprávy: příjemce, zobrazované jméno, předmět, tělo zprávy, počet skutečných SMTP SEND pokusů a výsledek. Tabulku můžeš označit a zkopírovat pomocí Ctrl+C. U prázdného těla se zobrazí `[prázdné tělo]`. Přehled je pouze v paměti aplikace a nový běh jej nahradí; zatím se samostatně neukládá do CSV. Před kopírováním nebo sdílením zkontroluj, zda neobsahuje citlivé údaje.

## 9. Mailbox Monitor (IMAP)

IMAP je volitelný. Typicky se používá port 993 s ImplicitTls nebo port 143 se StartTls. Přihlašovací údaje IMAP zůstávají pouze v paměti aplikace a neukládají se do profilu.

Po kliknutí na **Test / Obnovit** se načtou dostupné údaje kvóty a složek. Monitor může zobrazit celkovou STORAGE kvótu, počet zpráv podle kvóty, INBOX, Junk/Spam a velikost INBOX. Rozsah závisí na podpoře serveru a oprávněních účtu.

Volba **Zastavit SMTP test při dosažení kvóty** používá pouze skutečnou serverem oznámenou IMAP STORAGE kvótu. Kontrola probíhá po 30 sekundách, takže mezi kontrolami může dojít k překročení limitu. Bez skutečné kvóty se automatické zastavení ani mailbox feedback neaktivují.

## 10. Grafy a průběh

Na záložce **Test** jsou dva grafické pohledy:

- **SMTP / IMAP**: SEND pokusy/min, SMTP Accepted/min, 4xx/min, 5xx/min, nezávislá změna počtu zpráv podle IMAP kvóty/min a procento využití kvóty.
- **Adaptivní řízení**: poslední efektivní globální SEND interval a aktuální limit adaptivní souběžnosti.

Vzorky grafů se omezují přibližně na jeden za sekundu a historie je omezena na 120 vzorků. Rychlosti SMTP se počítají z přibližně desetisekundového klouzavého okna. Rychlost IMAP Δ/min se počítá jako průměr mezi dvěma odlišnými úspěšnými IMAP snapshoty a poslední vypočtená hodnota se drží do dalšího snapshotu; při chybě IMAP se vykreslí mezera. Hodnota intervalu 0 ms znamená, že není nastaven pevný časový strop. Graf adaptivní souběžnosti ukazuje povolený limit, nikoli počet právě aktivních síťových přenosů.

IMAP změna je nezávislé pozorování schránky. Může ji ovlivnit jiný klient nebo souběžná aktivita a není důkazem, že zprávy uložil právě tento test. Chybějící IMAP data se zobrazují jako mezera, nikoli nula.

## 11. Profily, logy a export

- **Uložit profil / Načíst profil** pracuje s JSON konfigurací. Hesla a IMAP přihlašovací údaje do profilu neukládej.
- **Export report JSON** exportuje poslední běh; report neobsahuje hesla a známé tajné hodnoty. Před sdílením jej přesto zkontroluj.
- Hlavní aplikační log je pouze v paměti a při novém běhu se vymaže.
- SMTP grid uchovává nejvýše 1 000 řádků.
- Volitelný SMTP session log se rotuje při 8 MiB a uchovává nejvýše tři zálohy.

## 12. Zastavení a zavření

Tlačítko **STOP** požádá o zrušení běhu. Již předané SMTP zprávy nelze vzít zpět. Při zavírání aplikace během běhu program nejprve požádá o zrušení a čeká na dokončení úklidu běžících operací.

### Google OAuth – první přihlášení

Klikni na **Přihlásit přes Google**, v systémovém prohlížeči vyber účet a potvrď oprávnění k odesílání e-mailů. Aplikace používá veřejné Desktop Client ID a PKCE; není potřeba vytvářet, kopírovat ani zadávat Client Secret. Obnovovací token se ukládá zašifrovaně pomocí Windows DPAPI pro aktuální účet Windows. Pokud je OAuth projekt v režimu Testing, musí být účet přidaný mezi testovací uživatele a platí omezení testovacího režimu.
