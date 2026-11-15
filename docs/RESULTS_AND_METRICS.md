# Výsledky a metriky

Tato stránka vysvětluje, co MailLoadTester skutečně měří a co z dostupných dat naopak nelze tvrdit.

## 1. SMTP Accepted není uložení do schránky

SMTP odpověď 2xx po příkazu DATA znamená, že SMTP server zprávu přijal. Neprokazuje, že zpráva:

- byla uložena do cílové schránky;
- skončila v Inboxu místo Junk/Spam;
- zůstala ve schránce po následném filtrování;
- zvýšila kvótu právě o velikost MIME zprávy.

Proto aplikace odděluje SMTP přijetí od nezávislých IMAP pozorování.

## 2. Počty

| Metrika | Význam |
|---|---|
| Sent / SMTP Accepted | Logické zprávy přijaté SMTP serverem (2xx) |
| Failed | Logické zprávy, které skončily neúspěchem |
| Retries | Opakované pokusy podle retry politiky |
| SMTP SEND attempts | Počet skutečně zahájených volání SMTP SendAsync, včetně retry |
| 4xx | Dočasné SMTP odpovědi |
| 5xx | Trvalé SMTP odpovědi |
| Throughput | Přijaté logické zprávy za dobu běhu; nejde o důkaz uložení |
| Latency | Naměřené časy operací; interpretuj je spolu s histogramem a typem zpráv |

SMTP 4xx obvykle znamená dočasný stav, například throttling, greylisting nebo dočasnou nedostupnost. SMTP 5xx obvykle znamená trvalé odmítnutí nebo problém s politikou/adresou. Permanentní odmítnutí 5xx se nemá slepě opakovat.

Příklady enhanced status kódů:

- **5.2.2** – schránka příjemce je plná;
- **5.3.4** – zpráva je příliš velká;
- **5.7.x** – bezpečnostní/politické odmítnutí.

## 3. Velikosti: MIME, síť a uložená data

Tyto hodnoty nejsou zaměnitelné:

1. **Připravená MIME velikost** – serializovaná zpráva po sestavení a pluginech, kterou Capacity Run měří před SMTP SEND.
2. **SMTP wire bytes** – skutečné bajty přenesené po síti; bez samostatného měření zůstávají Unknown.
3. **Mailbox STORAGE** – velikost uložená serverem podle IMAP kvóty; může se lišit od MIME velikosti kvůli internímu formátu, indexům, deduplikaci, kompresi a dalším pravidlům poskytovatele.

Base64 zvětšuje binární přílohu přibližně o třetinu a MIME přidává další režii. Předběžný odhad je proto orientační; runtime guard používá skutečně připravenou MIME velikost.

## 4. Capacity Run

Capacity Run může omezit:

- logický počet zpráv;
- součet připravených MIME bajtů napříč SEND pokusy;
- maximální MIME velikost jedné zprávy;
- globální počet SEND pokusů za minutu.

Každý opakovaný SEND pokus spotřebuje rozpočet znovu. To je záměrně konzervativní: při timeoutu nemusí být jisté, kolik dat už server obdržel.

SMTP `SIZE` je limit jedné zprávy oznámený SMTP serverem. Není to celková kvóta mailboxu.

## 5. IMAP kvóta a pozorované změny

IMAP může poskytnout STORAGE/MESSAGE kvótu a statistiky složek. Ne každý server podporuje všechny položky.

- **STORAGE quota** vyjadřuje využití úložiště a může být použita pro upozornění, zastavení nebo opatrný mailbox feedback.
- **MESSAGE quota** omezuje počet zpráv, pokud ji server podporuje.
- **INBOX size/count** popisuje jednu složku, nikoli nutně celý účet.
- **Junk/Spam** je samostatná složka; její zjištění závisí na IMAP metadatech a názvech složek.

Pokud server kvótu neposkytne, stav je **Unknown**. Aplikace nesmí neznámou hodnotu převést na nulu.

Rozdíl IMAP před a po testu je pouze pozorovaná změna. Může zahrnovat souběžnou aktivitu jiného klienta, pravidla serveru, mazání nebo přesuny zpráv. Nelze jej celý připsat testu.

## 6. Graf rychlostí

SMTP/IMAP graf zobrazuje:

- SMTP SEND pokusy za minutu;
- SMTP Accepted za minutu;
- SMTP 4xx a 5xx za minutu;
- pozorovanou změnu počtu zpráv podle IMAP kvóty za minutu;
- absolutní využití IMAP STORAGE kvóty v procentech.

Vzorky grafu jsou omezeny přibližně na jeden za sekundu a historie na 120 vzorků. SMTP rychlosti se počítají z přibližně desetisekundového klouzavého okna. IMAP Δ/min je průměrná změna mezi dvěma odlišnými serverovými snapshoty (typicky s odstupem 30 sekund); poslední hodnota se drží do dalšího snapshotu. Při neúspěšném IMAP měření nebo chybějících datech se zobrazí mezera, nikoli nula. Po resetu SMTP čítačů na hranici AutoRestart se může zobrazit mezera; ta je přesnější než vykreslit zápornou rychlost.

IMAP `Δ/min` je nezávislé pozorování změny schránky, nikoli počet zpráv prokazatelně uložených tímto během. Procento kvóty je absolutní stav serveru, nikoli rychlost.

## 7. Adaptivní řízení

Druhý graf ukazuje:

- poslední efektivní globální interval SEND v ms, včetně warm-up, jitter a mailbox-pressure floor;
- aktuální limit adaptivní souběžnosti.

Interval 0 ms znamená, že není nastaven pevný časový strop. Při kladném intervalu je orientační intervalový strop `60 000 / interval_ms` SEND za minutu; skutečný throughput může být nižší kvůli latenci a souběžnosti.

Adaptivní souběžnost je povolený limit souběžných workerů, nikoli počet právě aktivních SMTP přenosů. Nikdy nepřekročí nastavené maximum.

## 8. Jak výsledky interpretovat

Pro odhad kapacity schránky je vhodné kombinovat:

1. serverem oznámenou STORAGE/MESSAGE kvótu;
2. výchozí a konečný IMAP snapshot;
3. SMTP odpovědi a jejich enhanced status kódy;
4. připravenou MIME velikost;
5. jasně omezený test na autorizované schránce.

Samotný počet SMTP 2xx ani throughput nestačí k tvrzení, kolik zpráv schránka skutečně uložila.
