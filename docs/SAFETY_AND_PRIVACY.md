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
- SMTP a proxy hesla se neukládají do běžného profilu. IMAP přihlašovací údaje zůstávají v paměti aplikace.
- Sdílený profil může přesto obsahovat hosty, adresy, cesty k souborům a další provozní konfiguraci; před sdílením jej zkontroluj.

## 6. Logy, reporty a webhooky

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

## 7. Data a grafy

SMTP Accepted není důkaz uložení do schránky. IMAP delta může zahrnovat aktivitu jiných klientů. Chybějící kvóta je Unknown, ne nula. Tyto rozdíly jsou bezpečnostní i metodický požadavek, nikoli kosmetické označení.
