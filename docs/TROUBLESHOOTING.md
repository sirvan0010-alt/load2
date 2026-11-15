# Řešení problémů

## SMTP připojení selže

1. Ověř host a port.
2. Ověř, že port odpovídá TLS režimu: 587 obvykle StartTls, 465 ImplicitTls, 25 podle serveru.
3. Ověř DNS, firewall, VPN, proxy a odchozí pravidla sítě.
4. Port 25 bývá v některých sítích blokovaný. Direct MX vyžaduje přímé spojení na MX cílové domény.
5. Použij **Otestovat připojení**; tato akce neodesílá zprávu.

## TLS / certifikát

- Zkontroluj systémový čas a platnost certifikátu.
- Zkontroluj název hostu, port a režim TLS.
- Neřeš chybu vypnutím validace certifikátu v běžném provozu. Ignorování certifikátu je pouze dočasný diagnostický prostředek v izolovaném testu.

## Autentizace selže

- Ověř uživatelské jméno a metodu autentizace.
- U OAuth2 použij platný access token, nikoli refresh token.
- Někteří poskytovatelé vyžadují app-password nebo specifické povolení SMTP AUTH.
- Nepřeposílej do issue hesla, tokeny ani AUTH protokolové řádky.

## SMTP 4xx

4xx je dočasné odmítnutí. Může jít o throttling, greylisting, dočasnou nedostupnost nebo přechodný limit.

- Sniž paralelismus nebo zvyš globální interval.
- Zkontroluj Retry a backoff.
- Při 421/450/451/452/454 respektuj odklad a limity poskytovatele.
- Nezvyšuj rychlost ve snaze chybu překonat.

## SMTP 5xx

5xx je obvykle trvalé odmítnutí. Zkontroluj adresu, autentizaci, politiku a obsah.

- **5.2.2**: schránka je plná; ověř IMAP STORAGE/MESSAGE kvótu.
- **5.3.4**: zpráva překročila limit; zmenši přílohy nebo MIME obsah a ověř SMTP SIZE.
- **5.7.x**: bezpečnostní/politické odmítnutí; zjisti důvod u provozovatele. Neobcházej jej změnou IP nebo identity.

## Capacity Run se zastavil

Zkontroluj uvedený stop reason a tyto hodnoty:

- Capacity: max. zpráv;
- celkový MIME limit;
- max. MIME zpráva;
- max. zpráv/min;
- SMTP SIZE oznámený serverem.

Retry pokusy se započítávají do celkového MIME rozpočtu znovu. MIME rozpočet není totéž co velikost uložená v mailboxu.

## Úvodní validace selhala

Pokud je zapnutá startup validation, chyba jediné validační zprávy zastaví běh ještě před startem workerů. Zkontroluj první adresu v poli Komu, připojení, autentizaci a odpověď SMTP. Program validační zprávu automaticky neopakuje.

## IMAP kvóta se nezobrazuje

- Ověř IMAP host, port a TLS režim (993 ImplicitTls, 143 obvykle StartTls).
- Ověř přihlašovací údaje a IMAP oprávnění.
- Některé servery neposkytují STORAGE/MESSAGE quota nebo STATUS SIZE.
- Pokud server kvótu nepodporuje, stav zůstane Unknown. Aplikace nesmí odhad vydávat za skutečnou kvótu.

## Graf obsahuje mezery nebo nečekané změny

- Chybějící IMAP vzorek znamená, že server údaj neposkytl nebo kontrola nebyla úspěšná; nejde o nulu.
- SMTP počítadla se mohou na hranici AutoRestart resetovat. Graf v takovém místě raději vykreslí mezeru.
- IMAP Δ/min je nezávislé pozorování změny schránky. Mohou ji ovlivnit jiné klienty nebo pravidla serveru.
- Adaptivní graf ukazuje efektivní globální interval a povolený limit souběžnosti, nikoli skutečný počet aktivních SMTP přenosů.

## Logování

SMTP session log je ve výchozím stavu vypnutý. Při zapnutí může obsahovat adresy, hlavičky a obsah zpráv. Před sdílením logu jej zkontroluj a případné citlivé údaje odstraň.
