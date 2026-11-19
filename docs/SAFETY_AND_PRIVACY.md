# Bezpečnost a soukromí

MailLoadTester je určen pro řízené testování SMTP infrastruktury, účtů a schránek, k jejichž zatěžování máš výslovné oprávnění.

## 1. Rozsah oprávnění

- Testuj pouze vlastní nebo výslovně autorizované SMTP servery, účty, domény a příjemce.
- Veřejné poskytovatele (např. Gmail, Microsoft 365 nebo jiné veřejné služby) nezatěžuj bez jejich výslovného souhlasu.
- Přímé MX doručování, proxy, Source IP ani IP rotace nejsou oprávněním testovat cizí infrastrukturu.
- Nepoužívej aplikaci k obcházení antispamových filtrů, reputačních kontrol, rate limitů nebo politik poskytovatele.
- Nastav realistické limity, sleduj odpovědi a respektuj požadavky provozovatele.

## 2. Dry-run a Test Mode

- **Dry-run** simuluje průběh bez skutečné SMTP komunikace.
- **Test Mode** omezuje cílové domény, ale sám o sobě nezakazuje skutečné odesílání.
- **Otestovat připojení** ověřuje spojení a případně autentizaci; neodesílá testovací zprávu.
- **START** může zahájit skutečné odesílání, pokud není zapnutý Dry-run.

Před prvním reálným testem zkontroluj příjemce, počet zpráv, paralelismus, interval, retry a Capacity limity.

## 3. Úvodní validační zpráva

Volitelná startup validation posílá jednu skutečnou malou zprávu prvnímu příjemci ještě před spuštěním workerů. Je zahrnuta do počtu zpráv a Capacity Run rozpočtů. Při chybě se hlavní zátěžový test nespustí.

Zapínej ji pouze pro autorizovanou testovací schránku. SMTP 2xx potvrzuje přijetí serverem, nikoli uložení do schránky.

## 4. Limity a zastavení

- Paralelismus je počet souběžných workerů; vysoká hodnota může zvýšit zatížení serveru i lokálního počítače.
- Globální SEND interval a Capacity: max. zpráv/min jsou rychlostní limity.
- Capacity MIME limity chrání před příliš velkými zprávami a souhrnným objemem.
- Retry zvyšuje počet SEND pokusů a znovu spotřebovává MIME rozpočet.
- STOP požádá o zrušení, ale již předané zprávy nelze vzít zpět.
- IMAP quota polling probíhá v intervalu přibližně 30 sekund; mezi kontrolami může být limit překročen.

Při 4xx zpomal a zjisti příčinu. Při 5xx/policy odmítnutí test nezvyšuj ani nezkoušej obcházet blokaci.

## 5. TLS a přihlašovací údaje

- Používej správný port a režim TLS.
- **Ignorovat chyby certifikátu** nech vypnuté mimo izolovanou diagnostiku.
- Neposílej hesla, OAuth access tokeny ani hesla certifikátů do issue, screenshotů nebo veřejných logů.
- Běžné uložení profilu odstraní hlavní SMTP heslo, heslo proxy, heslo klientského certifikátu i hesla všech SMTP účtů. Po načtení je nutné přihlašovací údaje znovu doplnit.
- IMAP přihlašovací údaje zůstávají v paměti aplikace a do profilu se neukládají.
- Sdílený profil může přesto obsahovat hosty, adresy, cesty k souborům a další provozní konfiguraci; před sdílením jej zkontroluj.

## 6. Gmail SMTP a volitelné Google OAuth

- Výchozí cesta pro Gmail je SMTP + Heslo aplikace Google. Běžné heslo Google do SMTP klienta nezadávej. Heslo aplikace vyžaduje dvoufázové ověření a není dostupné u všech typů účtů.
- Google OAuth/Gmail API je samostatná pokročilá funkce. Používá Authorization Code + PKCE a oprávnění `gmail.send` spolu s OpenID Connect identitou. Není součástí běžného SMTP toku.
- OAuth přihlášení není v této verzi ověřené end-to-end; známý pokus vrátil `invalid_request: client_secret is missing`. Nepovažuj jej za garantovanou cestu přihlášení.
- Desktopový klient je veřejný: používá veřejné Client ID a Authorization Code + PKCE. Aplikace nevyžaduje ani neposílá Client Secret; PKCE chrání autorizační kód.
- Obnovovací token se ukládá samostatně, zašifrovaný Windows DPAPI CurrentUser. Jiný účet Windows jej nemůže běžně dešifrovat. Access token zůstává v paměti procesu.
- Odpojení požádá Google o odvolání refresh tokenu a odstraní lokální uložené údaje.
- Gmail API odesílání je oddělené od SMTP runneru. Nejde o SMTP test a nesmí se vykazovat jako SMTP throughput, SMTP AUTH nebo SMTP SEND.
- Gmail API nepoužívej k obcházení limitů Gmailu. Respektuj kvóty, zásady proti zneužití a případná omezení účtu.
- OAuth projekt v režimu Testing dovoluje pouze uvedené testovací účty; pro veřejnou distribuci je nutné produkční publikování a ověření citlivého scope `gmail.send` podle pravidel Googlu.
- Pokud API vrátí HTTP 401, aplikace může jednou obnovit token a opakovat požadavek. Při timeoutu nebo jiné nejednoznačné síťové chybě se odeslání automaticky neopakuje, aby nevznikly duplicitní zprávy.

## 7. Logy, reporty a webhooky

### SMTP session log

Je standardně vypnutý. Pokud jej zapneš, může obsahovat adresy, hlavičky, části obsahu a protokolové údaje. Známá autentizační tajemství se redigují, ale log přesto považuj za důvěrný.

Aktivní soubor má limit 8 MiB a uchovávají se nejvýše tři zálohy. Rotace neznamená anonymizaci.

### Ostatní logy

- Hlavní aplikační log je v paměti a při novém běhu se vymaže; jeho velikost je omezena.
- SMTP log grid uchovává nejvýše 1 000 posledních řádků.
- Volitelný HTTP Dashboard zpřístupni pouze lokálně; nevystavuj port do internetu nebo nedůvěryhodné sítě.

### JSON report

Export reportu neobsahuje hesla a známé tajné hodnoty. Před sdílením zkontroluj e-mailové adresy, hosty, IP adresy, cesty, hlavičky a další provozní metadata.

### Webhook

Webhook může odeslat výsledek běhu na nakonfigurovanou URL. Používej pouze důvěryhodný endpoint a ověř, jaké údaje přijímá a kam je ukládá.

## 8. SOCKS5 / Tor: co je chráněno a co ne

Tor je prostředník mezi počítačem a cílovým serverem.

- Když se SMTP spojení naváže přes funkční Tor SOCKS5 proxy, cílový SMTP server vidí jako zdroj síťovou adresu výstupního uzlu Tor, nikoli přímo veřejnou IP adresu domácího připojení.
- Když Tor Browser nebo Tor služba přestane fungovat, spojení přes SOCKS5 obvykle selže. Aplikace musí odesílání zastavit; nesmí zkusit přímé připojení jako náhradní cestu.
- MailLoadTester používá pro SMTP nakonfigurovanou proxy. V tomto režimu není záměrná automatická záložní cesta přes běžné internetové připojení. Pokud proxy není dostupná, SMTP spojení se nemá podařit.
- Zelený indikátor znamená, že kontrolní požadavek přes SOCKS5 potvrdil Tor exit. Není to nepřetržitý důkaz pro každý paket ani záruka anonymity.
- Během běhu se ověření Toru opakuje. Pokud kontrola selže, aktivní test se zruší. Již předané zprávy nelze vzít zpět; zprávy, které SMTP server ještě nepřijal, se nemusí odeslat.

**Tor chrání síťovou IP, ne identitu e-mailového účtu.** Pokud se přes Tor přihlásíš ke svému osobnímu Gmailu a odešleš zprávu, Google stále ví, který účet používáš. Příjemce obvykle uvidí skutečnou e-mailovou adresu účtu v hlavičce From, i když nastavíš jiné zobrazované jméno. Náhodné jméno mění pouze text zobrazený vedle adresy; nemění účet, přihlašovací údaje ani vlastnictví schránky.

Příklad: přihlásíš se k účtu `mojeadresa@gmail.com` a nastavíš zobrazované jméno `Alex Morgan`. Příjemce může vidět „Alex Morgan“, ale adresa účtu zůstane `mojeadresa@gmail.com` a Google zná účet, ze kterého zpráva odešla.

Tor proto nepovažuj za anonymizaci osobního Gmail účtu. Jde o ochranu síťové cesty, nikoli o skrytí identity odesílatele.

## 9. Data a grafy

SMTP Accepted není důkaz uložení do schránky. IMAP delta může zahrnovat aktivitu jiných klientů. Chybějící kvóta je Unknown, ne nula. Tyto rozdíly jsou bezpečnostní i metodický požadavek, nikoli kosmetické označení.
