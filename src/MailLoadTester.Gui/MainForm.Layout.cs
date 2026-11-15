namespace MailLoadTester.Gui;

public sealed partial class MainForm
{

    private void InitializeComponent()
    {
        toolTip = new ToolTip { AutoPopDelay = 3000, InitialDelay = 150, ReshowDelay = 50, ShowAlways = true };

        // === UI enhancement ===
        pnlSafetyBanner = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(12, 6, 12, 6),
            BorderStyle = BorderStyle.FixedSingle
        };
        lblSafetyBanner = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        pnlSafetyBanner.Controls.Add(lblSafetyBanner);

        btnWizard = new Button
        {
            Text = "GETTING STARTED",
            Width = 150,
            Height = 36,
            Left = 710,
            Top = 6,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnWizard.FlatAppearance.BorderSize = 0;

        chkDarkMode = new CheckBox
        {
            Text = "Dark mode",
            AutoSize = true,
            Left = 880,
            Top = 16
        };

        // === Top button bar ===
        var topPanel = new Panel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8, 6, 8, 6) };
        btnStart = new Button
        {
            Text = "▶ START", Width = 120, Height = 36, Left = 8, Top = 6,
            BackColor = Color.FromArgb(34, 139, 34), ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btnStart.FlatAppearance.BorderSize = 0;
        btnStop = new Button
        {
            Text = "■ STOP", Width = 120, Height = 36, Left = 140, Top = 6,
            BackColor = Color.FromArgb(178, 34, 34), ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat, Enabled = false, Cursor = Cursors.Hand
        };
        btnStop.FlatAppearance.BorderSize = 0;
        topPanel.Controls.Add(btnStart);
        topPanel.Controls.Add(btnStop);
        btnSaveProfile = new Button
        {
            Text = "💾 Uložit profil", Width = 130, Height = 36, Left = 280, Top = 6,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btnLoadProfile = new Button
        {
            Text = "📂 Načíst profil", Width = 130, Height = 36, Left = 420, Top = 6,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
        };
        btnExportReport = new Button
        {
            Text = "Export report JSON", Width = 140, Height = 36, Left = 560, Top = 6,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Enabled = false
        };
        toolTip.SetToolTip(btnStart, "Spustí test podle aktuální konfigurace. Pokud není zapnutý Dry-run, může dojít ke skutečnému odesílání e-mailů.");
        toolTip.SetToolTip(btnStop, "Požádá o bezpečné zastavení právě běžícího testu. Již předané zprávy nelze vzít zpět.");
        toolTip.SetToolTip(btnSaveProfile, "Uloží aktuální nastavení testu do JSON (bez hesel).");
        toolTip.SetToolTip(btnLoadProfile, "Načte profil testu z JSON souboru.");
        toolTip.SetToolTip(btnExportReport, "Exportuje poslední běh jako JSON report bez hesel a známých tajných hodnot.");
        toolTip.SetToolTip(btnWizard, "Zobrazí stručného průvodce prvním bezpečným testem.");
        toolTip.SetToolTip(chkDarkMode, "Přepne vzhled aplikace mezi světlým a tmavým režimem.");
        topPanel.Controls.Add(btnSaveProfile);
        topPanel.Controls.Add(btnLoadProfile);
        topPanel.Controls.Add(btnExportReport);
        topPanel.Controls.Add(btnWizard);
        topPanel.Controls.Add(chkDarkMode);
        Controls.Add(topPanel);
        Controls.Add(pnlSafetyBanner);

        // === TabControl ===
        tabs = new TabControl { Dock = DockStyle.Fill };

        // --- Tab: SMTP Server ---
        var tabSmtp = new TabPage("SMTP Server") { Padding = new Padding(12) };
        var tblSmtp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tblSmtp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblSmtp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var gbConn = new GroupBox { Text = "Připojení", Dock = DockStyle.Fill };
        var flConn = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtHost = AddLabeledText(flConn, "SMTP Host:", "Např. smtp.gmail.com nebo IP adresa serveru.");
        numPort = AddLabeledNumeric(flConn, "Port:", 1, 65535, 587, "25=plain, 587=STARTTLS, 465=Implicit TLS");
        cmbSecurity = AddLabeledCombo(flConn, "Zabezpečení:", ["None", "StartTls", "ImplicitTls"], "StartTls", "None=bez šifrování, StartTLS=TLS po EHLO, ImplicitTLS=SSL hned po connectu.");
        chkIgnoreCert = AddCheckBox(flConn, "Ignorovat chyby certifikátu", false, "Pouze pro testování – nepoužívej v produkci!");
        txtSourceIp = AddLabeledText(flConn, "Source IP (volitelné):", "Váže SMTP socket na konkrétní lokální IP. Můžeš vybrat ze seznamu níže.");
        var lblLocalIp = new Label { Text = "Lokální IPv4 (výběr):", AutoSize = true, Margin = new Padding(0, 6, 0, 2) };
        flConn.Controls.Add(lblLocalIp);
        cmbLocalIp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        cmbLocalIp.Items.Add("(automaticky – systém)");
        try
        {
            foreach (var ip in NetworkAdapterInfo.GetLocalIPv4Addresses())
                cmbLocalIp.Items.Add(ip);
        }
        catch { /* ignore */ }
        cmbLocalIp.SelectedIndex = 0;
        cmbLocalIp.SelectedIndexChanged += (s, e) =>
        {
            if (cmbLocalIp.SelectedIndex > 0)
                txtSourceIp.Text = cmbLocalIp.SelectedItem?.ToString() ?? "";
            else if (cmbLocalIp.SelectedIndex == 0)
                txtSourceIp.Text = "";
        };
        toolTip.SetToolTip(lblLocalIp, "Vyber lokální IPv4 adresu počítače, ze které se budou navazovat SMTP spojení.");
        toolTip.SetToolTip(cmbLocalIp, "Vybere lokální IPv4 adresu pro IP binding (Source IP). Automatická volba ponechá výběr na operačním systému.");
        flConn.Controls.Add(cmbLocalIp);
        txtIpv6Prefix = AddLabeledText(flConn, "IPv6 prefix (rotace):",
            "Např. 2001:db8:85a3:0:: — každé nové SMTP spojení dostane náhodnou adresu z /N. OS musí prefix routovat. Prázdné = vypnuto. Má přednost před Source IP.");
        numIpv6PrefixLen = AddLabeledNumeric(flConn, "IPv6 prefix length:", 1, 128, 64,
            "Délka prefixu v bitech (běžně 64).");
        txtIpv4Rotation = AddLabeledText(flConn, "IPv4 rotace (seznam/CIDR):",
            "Příklady: 192.0.2.10,192.0.2.11  nebo  192.0.2.0/28. Každé NOVÉ SMTP spojení použije další adresu. Musí existovat na síťové kartě. Prázdné = vypnuto. Priorita: IPv6 rotace > IPv4 rotace > Source IP.");
        chkIpv4RotationRandom = AddCheckBox(flConn, "IPv4 rotace náhodně (ne round-robin)", false,
            "Zapnuto = náhodná adresa z poolu. Vypnuto = postupně dokola.");
        cmbIpVersion = AddLabeledCombo(flConn, "IP verze:", ["Any", "IPv4Only", "IPv6Only", "DualStack"], "Any", "Výběr IP verze pro spojení.");
        gbConn.Controls.Add(flConn);
        tblSmtp.Controls.Add(gbConn, 0, 0);

        var gbAuth = new GroupBox { Text = "Autentizace & Certifikáty", Dock = DockStyle.Fill };
        var flAuth = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkAuth = AddCheckBox(flAuth, "Použít autentizaci", false, "Zaškrtni, pokud server vyžaduje jméno a heslo.");
        cmbAuthMethod = AddLabeledCombo(flAuth, "Auth metoda:", ["Auto", "Plain", "Login", "CramMd5", "ScramSha1", "Ntlm", "OAuth2"], "Auto", "Výběr SMTP autentizační metody. Auto zvolí nejlepší podle serveru. OAuth2: do pole Heslo vlož access token (ne refresh token).");
        txtUser = AddLabeledText(flAuth, "Uživatel:", "SMTP username (často celý e-mail).");
        txtPass = AddLabeledText(flAuth, "Heslo:", "SMTP heslo, app-password, nebo při OAuth2 access token.");
        txtPass.PasswordChar = '●';
        txtClientCert = AddLabeledText(flAuth, "Client cert (.pfx):", "Cesta k klientskému certifikátu pro mTLS.");
        txtClientCertPass = AddLabeledText(flAuth, "Heslo certifikátu:", "Heslo k .pfx souboru (pokud je zašifrovaný).");
        txtClientCertPass.PasswordChar = '●';
        numConnectTimeout = AddLabeledNumeric(flAuth, "Connect timeout (ms):", 1000, 300000, 20000, "Max čas na navázání TCP spojení.");
        numReadTimeout = AddLabeledNumeric(flAuth, "Read timeout (ms):", 1000, 300000, 20000, "Max čas na čekání na odpověď serveru.");
        btnTestConnection = new Button { Text = "🖧 Otestovat připojení", Width = 180, Height = 32, Margin = new Padding(0, 8, 0, 0) };
        flAuth.Controls.Add(btnTestConnection);
        toolTip.SetToolTip(btnTestConnection, "Ověří TCP/TLS spojení a případnou SMTP autentizaci, ale neodešle zprávu.");
        btnSendTest = new Button
        {
            Text = "Odeslat 1 testovací e-mail", Width = 230, Height = 36,
            Margin = new Padding(0, 8, 0, 0), FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(38, 105, 170), ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        btnSendTest.FlatAppearance.BorderSize = 0;
        flAuth.Controls.Add(btnSendTest);
        toolTip.SetToolTip(btnSendTest, "Po potvrzení odešle jednu skutečnou testovací zprávu na první adresu v poli Komu. Nepoužívá zátěžový režim.");
        gbAuth.Controls.Add(flAuth);
        tblSmtp.Controls.Add(gbAuth, 1, 0);

        tabSmtp.Controls.Add(tblSmtp);
        tabs.TabPages.Add(tabSmtp);
        InitAccountsTab(tabs);

        // --- Tab: Zpráva ---
        var tabMsg = new TabPage("Zpráva") { Padding = new Padding(12) };
        var tblMsg = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tblMsg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblMsg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var gbAddr = new GroupBox { Text = "Adresy", Dock = DockStyle.Fill };
        var flAddr = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtFrom = AddLabeledText(flAddr, "Od:", "Odesílatel (např. test@example.com).");
        txtTo = AddLabeledText(flAddr, "Komu:", "Příjemci oddělení čárkou nebo novým řádkem.");
        txtTo.Multiline = true; txtTo.Height = 50;
        txtCc = AddLabeledText(flAddr, "CC:", "Kopie — oddělené čárkou.");
        txtBcc = AddLabeledText(flAddr, "BCC:", "Skryté kopie — oddělené čárkou.");
        txtDisplayName = AddLabeledText(flAddr, "Zobrazované jméno:", "Jméno, které se zobrazí v mail klientu.");
        gbAddr.Controls.Add(flAddr);
        tblMsg.Controls.Add(gbAddr, 0, 0);

        var gbContent = new GroupBox { Text = "Obsah & Šablona", Dock = DockStyle.Fill };
        var flContent = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtSubject = AddLabeledText(flContent, "Předmět:",
            "Předmět e-mailu. Dynamické tagy: {TIMESTAMP}, {GUID}, {RANDOM_WORD}, {RANDOM_WORD:12}, {TEST_ID} – každá zpráva bude unikátní.");
        var lblBody = new Label { Text = "Tělo zprávy:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        flContent.Controls.Add(lblBody);
        txtBody = new TextBox { Multiline = true, Height = 80, Width = 360, ScrollBars = ScrollBars.Vertical };
        flContent.Controls.Add(txtBody);
        toolTip.SetToolTip(lblBody, "Obsah každé odesílané zprávy. Lze použít prostý text, HTML nebo dynamické značky.");
        toolTip.SetToolTip(txtBody, "Text e-mailu. HTML: zaškrtni HTML tělo. Inline obrázky: {{cid:nazev}}. Dynamické tagy: {TIMESTAMP}, {GUID}, {RANDOM_WORD}, {RANDOM_WORD:12}, {TEST_ID}.");
        chkHtmlBody = AddCheckBox(flContent, "HTML tělo", false, "Zpráva bude odeslána jako text/html.");
        chkRandomData = AddCheckBox(flContent, "Náhodná testovací data", false, "Automaticky vygeneruje náhodný předmět, tělo a jméno.");
        chkBogusData = AddCheckBox(flContent, "Realistická data (Bogus)", false,
            "Použije realistická jména, názvy produktů a text. Nemění From.");
        chkRandomHtml = AddCheckBox(flContent, "Náhodné HTML tělo", false,
            "Každá zpráva dostane synteticky generované HTML tělo.");
        chkRandomAttachments = AddCheckBox(flContent, "Náhodné přílohy", false,
            "Ke každé zprávě přidá 1 až zvolený počet testovacích souborů. Velikost lze nastavit; bezpečnost RAM se kontroluje před startem.");
        var pnlRandomAttachments = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        pnlRandomAttachments.Controls.Add(new Label { Text = "Max. náhodných příloh:", AutoSize = true, Padding = new Padding(0, 5, 4, 0) });
        numMaxRandomAttachments = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 2, Width = 55 };
        pnlRandomAttachments.Controls.Add(numMaxRandomAttachments);
        pnlRandomAttachments.Controls.Add(new Label { Text = "Velikost / příloha (MB):", AutoSize = true, Padding = new Padding(12, 5, 4, 0) });
        numRandomAttachmentSizeMb = new NumericUpDown { Minimum = 0, Maximum = 128, Value = 0, DecimalPlaces = 0, Width = 70 };
        pnlRandomAttachments.Controls.Add(numRandomAttachmentSizeMb);
        flContent.Controls.Add(pnlRandomAttachments);
        lblRandomAttachmentSafety = new Label { AutoSize = false, Width = 390, Height = 62, Text = "Bezpečnost příloh: Auto – výpočet podle RAM, počtu příloh a paralelismu.", Margin = new Padding(0, 2, 0, 6) };
        flContent.Controls.Add(lblRandomAttachmentSafety);
        toolTip.SetToolTip(numMaxRandomAttachments, "Maximální počet náhodně vytvořených příloh přidaných do jedné zprávy. Rozsah: 1 až 5; výchozí hodnota: 2. Vyšší počet zvyšuje velikost zprávy i spotřebu paměti.");
        toolTip.SetToolTip(numRandomAttachmentSizeMb, "0 = Auto. Program před startem odhadne špičku: velikost × počet příloh × paralelní workery + raw data + MIME/base64 režie. Pokud odhad překročí bezpečný rozpočet dostupné RAM, test se před alokací zastaví. Před každou velkou generovanou přílohou se RAM ještě znovu ověří, protože dostupná RAM se může během testu změnit. Ruční hodnota je v MB, maximum 128 MB.");
        toolTip.SetToolTip(lblRandomAttachmentSafety, "Bezpečnostní odhad není přesný měřič celé RAM. Je záměrně konzervativní. Test se při nebezpečné kombinaci nespustí, takže vysoká hodnota sama o sobě PC nezasekne.");
        chkVaryMessage = AddCheckBox(flContent, "Variace Subject/Body u každé zprávy", false,
            "Přidá jedinečné ID/token ke každé zprávě i bez plného random režimu.");
        chkSmtpUtf8 = AddCheckBox(flContent, "SMTPUTF8 (Unicode adresy)", false, "Povolí Unicode v e-mailových adresách (RFC 6531).");
        var lblEml = new Label { Text = "EML šablona:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        flContent.Controls.Add(lblEml);
        var pnlEml = new Panel { Width = 360, Height = 26 };
        txtEmlTemplate = new TextBox { Width = 280, Height = 23, Left = 0, Top = 0 };
        btnEmlBrowse = new Button { Text = "…", Width = 30, Height = 23, Left = 282, Top = 0 };
        pnlEml.Controls.Add(txtEmlTemplate);
        pnlEml.Controls.Add(btnEmlBrowse);
        flContent.Controls.Add(pnlEml);
        toolTip.SetToolTip(lblEml, "Volitelná šablona celé zprávy ve formátu EML.");
        toolTip.SetToolTip(txtEmlTemplate, "Načte předmět, tělo a přílohy ze souboru .eml místo ručního zadání.");
        toolTip.SetToolTip(btnEmlBrowse, "Vybere soubor .eml z disku.");
        gbContent.Controls.Add(flContent);
        tblMsg.Controls.Add(gbContent, 1, 0);

        var gbAttach = new GroupBox { Text = "Přílohy", Dock = DockStyle.Bottom, Height = 160 };
        var flAttach = new FlowLayoutPanel { Dock = DockStyle.Fill };
        lstAttachments = new ListBox { Width = 280, Height = 90 };
        flAttach.Controls.Add(lstAttachments);
        var btnAdd = new Button { Text = "Přidat…", Width = 80, Height = 26 };
        var btnRem = new Button { Text = "Odebrat", Width = 80, Height = 26 };
        flAttach.Controls.Add(btnAdd);
        flAttach.Controls.Add(btnRem);
        btnAdd.Click += (s, e) => { using var dlg = new OpenFileDialog { Multiselect = true }; if (dlg.ShowDialog() == DialogResult.OK) lstAttachments.Items.AddRange(dlg.FileNames); };
        btnRem.Click += (s, e) => { if (lstAttachments.SelectedIndex >= 0) lstAttachments.Items.RemoveAt(lstAttachments.SelectedIndex); };
        toolTip.SetToolTip(btnAdd, "Vybere jeden nebo více souborů, které se připojí ke každé testovací zprávě.");
        toolTip.SetToolTip(btnRem, "Odebere právě vybranou přílohu ze seznamu. Soubor na disku nesmaže.");
        toolTip.SetToolTip(lstAttachments, "Seznam souborů připojených ke každé zprávě.");
        gbAttach.Controls.Add(flAttach);
        tabMsg.Controls.Add(gbAttach);
        gbAttach.Dock = DockStyle.Bottom;

        var gbInline = new GroupBox { Text = "Inline přílohy (HTML CID)", Dock = DockStyle.Bottom, Height = 100 };
        var flInline = new FlowLayoutPanel { Dock = DockStyle.Fill };
        lstInlineAttachments = new ListBox { Width = 280, Height = 60 };
        flInline.Controls.Add(lstInlineAttachments);
        var btnAddInline = new Button { Text = "Přidat…", Width = 80, Height = 26 };
        var btnRemInline = new Button { Text = "Odebrat", Width = 80, Height = 26 };
        flInline.Controls.Add(btnAddInline);
        flInline.Controls.Add(btnRemInline);
        btnAddInline.Click += (s, e) => { using var dlg = new OpenFileDialog { Multiselect = true, Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp" }; if (dlg.ShowDialog() == DialogResult.OK) lstInlineAttachments.Items.AddRange(dlg.FileNames); };
        btnRemInline.Click += (s, e) => { if (lstInlineAttachments.SelectedIndex >= 0) lstInlineAttachments.Items.RemoveAt(lstInlineAttachments.SelectedIndex); };
        toolTip.SetToolTip(btnAddInline, "Vybere obrázky, které se vloží přímo do HTML zprávy jako inline obsah.");
        toolTip.SetToolTip(btnRemInline, "Odebere vybraný inline obrázek ze seznamu. Soubor na disku nesmaže.");
        toolTip.SetToolTip(lstInlineAttachments, "Obrázky vložené do HTML těla. V HTML použij {{cid:nazev_souboru_bez_pripony}}.");
        gbInline.Controls.Add(flInline);
        tabMsg.Controls.Add(gbInline);
        gbInline.Dock = DockStyle.Bottom;

        var gbHeaders = new GroupBox { Text = "Vlastní hlavičky (X-…)", Dock = DockStyle.Bottom, Height = 100 };
        txtHeaders = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 10) };
        txtHeaders.Text = "X-Test-ID: 123";
        toolTip.SetToolTip(txtHeaders, "Jedna hlavička na řádek. Formát: X-Něco: hodnota");
        gbHeaders.Controls.Add(txtHeaders);
        tabMsg.Controls.Add(gbHeaders);
        gbHeaders.Dock = DockStyle.Bottom;

        tabMsg.Controls.Add(tblMsg);
        tblMsg.Dock = DockStyle.Fill;
        tabs.TabPages.Add(tabMsg);

        // --- Tab: Test ---
        var tabTest = new TabPage("Test") { Padding = new Padding(12) };
        var tblTest = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        tblTest.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tblTest.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var gbParams = new GroupBox { Text = "Parametry", Dock = DockStyle.Fill };
        var flParams = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        numCount = AddLabeledNumeric(flParams, "Počet zpráv:", 1, 10000, 10, "Kolik zpráv celkem odeslat (1–10000).");
        numConcurrency = AddLabeledNumeric(flParams, "Paralelismus:", 1, 20, 1, "Kolik zpráv se odesílá současně (1–20).");
        numInterval = AddLabeledNumeric(flParams, "Interval (ms):", 0, 3600000, 0, "Minimální GLOBÁLNÍ rozestup mezi začátky SMTP SEND (ne per-worker). 0 = bez pevného časového stropu; rychlost pak omezuje paralelismus, příprava zprávy a odezva serveru. 1000 ms = nejvýše přibližně 1 nový SEND/s i při 20 workerech.");
        numRetries = AddLabeledNumeric(flParams, "Retry:", 0, 5, 0, "Kolikrát zkusit znovu při dočasné chybě (0–5).");
        chkCapacityRun = AddCheckBox(flParams, "Zapnout Capacity Run – bezpečnostní limity", false,
            "Zapne pevné limity běhu. MIME bajty se měří až po sestavení zprávy a pluginech, těsně před SMTP SEND. Souhrnný rozpočet započítává i opakované pokusy. Nejde o velikost uloženou ve schránce.");
        numCapacityMaxMessages = AddLabeledNumeric(flParams, "Capacity: max. zpráv:", 1, 10000, 1000,
            "Horní limit logických zpráv pro tento běh. Skutečný počet je nejvýše také hodnota Počet zpráv.");
        numCapacityTargetMiB = AddLabeledNumeric(flParams, "Capacity: celkový MIME limit (MiB):", 0, 1048576, 0,
            "Maximální součet serializovaných MIME bajtů napříč všemi SEND pokusy včetně retry. 0 = bez limitu.", decimalPlaces: 2);
        numCapacityMaxMessageMiB = AddLabeledNumeric(flParams, "Capacity: max. MIME zpráva (MiB):", 0, 1048576, 0,
            "Maximální serializovaná velikost jedné MIME zprávy. 0 = bez limitu. Aktivní SMTP SIZE limit se automaticky uplatní, pokud ho server po připojení oznámí.", decimalPlaces: 2);
        numCapacityMaxPerMinute = AddLabeledNumeric(flParams, "Capacity: max. zpráv/min:", 0, 60000, 0,
            "Pevný strop globálního tempa včetně retry. 0 = bez dodatečného stropu; nikdy nezrychlí již pomalejší interval.");
        chkCapacityStartupValidation = AddCheckBox(flParams, "Před testem odeslat 1 malou validační zprávu", false,
            "Výchozí vypnuto. Po výslovném spuštění odešle jednu malou zprávu prvnímu příjemci ještě před spuštěním workerů. Je zahrnuta do Počtu zpráv, Capacity: max. zpráv, MIME rozpočtu, SMTP pokusů i výsledků. Při chybě se hlavní zátěžový test vůbec nespustí; validační zpráva se automaticky neopakuje.");
        btnCapacityEstimate = new Button { Text = "Odhad MIME objemu", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        btnCapacityEstimate.Click += (_, _) =>
        {
            try { UpdateCapacityEstimate(BuildOptions()); }
            catch (Exception ex) { lblCapacityEstimate.Text = "Odhad nelze spočítat: " + ex.Message; }
        };
        lblCapacityEstimate = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(450, 0),
            ForeColor = Color.DimGray,
            Text = "Orientační MIME odhad se zobrazí po kliknutí na tlačítko.",
            Margin = new Padding(3, 2, 3, 8)
        };
        chkCapacityRun.CheckedChanged += (_, _) =>
        {
            try { UpdateCapacityEstimate(BuildOptions()); } catch { }
        };
        numCount.ValueChanged += (_, _) =>
        {
            try { UpdateCapacityEstimate(BuildOptions()); } catch { }
        };
        numCapacityMaxMessages.ValueChanged += (_, _) =>
        {
            try { UpdateCapacityEstimate(BuildOptions()); } catch { }
        };
        flParams.Controls.Add(btnCapacityEstimate);
        flParams.Controls.Add(lblCapacityEstimate);
        gbParams.Controls.Add(flParams);
        tblTest.Controls.Add(gbParams, 0, 0);

        var gbBatch = new GroupBox { Text = "Dávkový režim", Dock = DockStyle.Fill };
        var flBatch = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkBatchMode = AddCheckBox(flBatch, "Použít dávky", false, "Rozdělí zprávy do dávek s pauzou mezi nimi.");
        numBatchSize = AddLabeledNumeric(flBatch, "Velikost dávky:", 1, 1000, 10, "Kolik zpráv v jedné dávce.");
        numBatchPause = AddLabeledNumeric(flBatch, "Pauza (s):", 1, 86400, 1, "Kolik sekund počkat mezi dávkami.");
        gbBatch.Controls.Add(flBatch);

        var rightTestPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0)
        };
        rightTestPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rightTestPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
        rightTestPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rightTestPanel.Controls.Add(gbBatch, 0, 0);
        var chartTabs = new TabControl { Dock = DockStyle.Fill };
        var smtpMetricsPage = new TabPage("SMTP / IMAP") { Padding = new Padding(3) };
        runMetricsChart = new RunMetricsChart { Dock = DockStyle.Fill };
        toolTip.SetToolTip(runMetricsChart, "Živý graf rychlosti SMTP SEND pokusů, SMTP Accepted a chyb za minutu, počítané z klouzavého přibližně desetisekundového okna. IMAP Δ/min je nezávislé pozorování změny schránky, nikoli důkaz, že konkrétní zprávy uložil tento běh.");
        smtpMetricsPage.Controls.Add(runMetricsChart);
        var adaptiveMetricsPage = new TabPage("Adaptivní řízení") { Padding = new Padding(3) };
        adaptiveControlChart = new AdaptiveControlChart { Dock = DockStyle.Fill };
        toolTip.SetToolTip(adaptiveControlChart, "Historie efektivního globálního SEND intervalu a aktuálního limitu adaptivní souběžnosti. Interval 0 ms znamená, že není nastaven pevný časový strop; skutečnou rychlost dále omezuje souběžnost a odezva SMTP.");
        adaptiveMetricsPage.Controls.Add(adaptiveControlChart);
        chartTabs.TabPages.Add(smtpMetricsPage);
        chartTabs.TabPages.Add(adaptiveMetricsPage);
        rightTestPanel.Controls.Add(chartTabs, 0, 1);
        tblTest.Controls.Add(rightTestPanel, 1, 0);

        tabTest.Controls.Add(tblTest);
        tblTest.Dock = DockStyle.Fill;

        var gbMode = new GroupBox { Text = "Režim", Dock = DockStyle.Bottom, Height = 110 };
        var flMode = new FlowLayoutPanel { Dock = DockStyle.Fill };
        chkDryRun = AddCheckBox(flMode, "Dry-run (bez reálného SMTP)", false, "Simuluje odesílání bez spojení k serveru. Pipeline cesty je v dry-run jen ilustrativní, ne skutečná SMTP komunikace.");
        chkTestMode = AddCheckBox(flMode, "Test mode (omezit domény)", false, "Povolí odesílat jen na vybrané domény.");
        txtAllowedDomains = AddLabeledText(flMode, "Povolené domény:", "Seznam domén oddělených čárkou (např. example.com,test.cz).");
        gbMode.Controls.Add(flMode);
        tabTest.Controls.Add(gbMode);
        tabs.TabPages.Add(tabTest);

        // --- Tab: Proxy ---
        var tabProxy = new TabPage("Proxy") { Padding = new Padding(12) };
        var flProxy = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkSocks5 = AddCheckBox(flProxy, "Použít SOCKS5 proxy (jedna)", false, "Jedna SOCKS5 proxy níže. Pro více serverů použij seznam rotace.");
        txtProxyList = AddLabeledText(flProxy, "Proxy seznam (rotace):",
            "Jeden záznam na řádek nebo středník. Příklady: socks5://127.0.0.1:1080  |  http://user:pass@proxy:8080  |  host:1080");
        txtProxyHost = AddLabeledText(flProxy, "Proxy host:", "SOCKS5 proxy host (např. 127.0.0.1).");
        numProxyPort = AddLabeledNumeric(flProxy, "Proxy port:", 1, 65535, 1080, "SOCKS5 proxy port (Tor používá 9050).");
        txtProxyUser = AddLabeledText(flProxy, "Proxy uživatel:", "Volitelné — pouze pokud proxy vyžaduje autentizaci.");
        txtProxyPass = AddLabeledText(flProxy, "Proxy heslo:", "Volitelné heslo pro SOCKS5 proxy.");
        txtProxyPass.PasswordChar = '●';
        tabProxy.Controls.Add(flProxy);
        tabs.TabPages.Add(tabProxy);

        // --- Tab: Pokročilé ---
        var tabAdv = new TabPage("Pokročilé") { Padding = new Padding(12) };
        var flAdv = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        chkDirectMx = AddCheckBox(flAdv, "Direct MX delivery", false, "Místo relay SMTP se zeptá DNS na MX záznam a mluví přímo s cílovým serverem.");
        chkPreWarm = AddCheckBox(flAdv, "Pre-warm spojení", false, "Před testem vytvoří a ověří všechna spojení v poolu — první dávka nečeká na handshake.");
        chkAdaptive = AddCheckBox(flAdv, "Adaptive concurrency", false, "Začíná na nastaveném max. paralelismu. V přibližně 10s oknech při tlaku serveru (4xx/timeout/transport) paralelismus sníží; po zdravých oknech ho postupně obnoví nejvýše na nastavený počet workerů. Samo o sobě nezvyšuje rychlost nad globální interval a nikdy nepřekročí MaxConcurrency.");
        chkCircuit = AddCheckBox(flAdv, "Circuit breaker", false, "Po N po sobě jdoucích selháních stejného typu přestane zkoušet a vrátí výsledek.");
        numCircuitThreshold = AddLabeledNumeric(flAdv, "Circuit breaker threshold:", 1, 50, 5, "Kolik po sobě jdoucích selhání otevře jistič.");
        chkDashboard = AddCheckBox(flAdv, "HTTP Dashboard", false, "Spustí mini webový server na localhost — otevři prohlížeč a sleduj live grafy.");
        numDashboardPort = AddLabeledNumeric(flAdv, "Dashboard port:", 1, 65535, 5000, "Port pro live dashboard (výchozí 5000).");
        chkAutoRestart = AddCheckBox(flAdv, "Auto-restart při selhání", false, "POZOR: znovu odešle CELÝ test (může doručit zprávy vícekrát). Defaultně vypnuto. Snižuje paralelismus po většinovém selhání.");
        numAutoRestartAttempts = AddLabeledNumeric(flAdv, "Max auto-restartů:", 1, 10, 3, "Kolikrát zkusit restart při selhání.");
        chkSessionLog = AddCheckBox(flAdv, "SMTP session log", false, "Volitelné podrobné SMTP C:/S: logování, ve výchozím stavu vypnuté. Pozor: může obsahovat adresy, hlavičky a obsah zpráv; autentizační tajemství se redigují. Aktivní soubor má limit 8 MiB a uchovávají se nejvýše 3 zálohy (celkem přibližně 32 MiB).");
        txtSessionLogPath = AddLabeledText(flAdv, "Session log soubor:", "Cílová cesta SMTP session logu. Při novém běhu se předchozí soubor přesune do .1 až .3; během běhu se soubor automaticky rotuje při 8 MiB. Starší zálohy nad limit se odstraní.");
        tabAdv.Controls.Add(flAdv);
        tabs.TabPages.Add(tabAdv);

        // --- Tab: Síť / integrace ---
        var tabExp = new TabPage("Síť / integrace") { Padding = new Padding(12) };
        var flExp = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        txtWebhookUrl = AddLabeledText(flExp, "Webhook URL:", "Po testu automaticky POSTne JSON výsledek na tuto URL (Slack, Teams, CI/CD).");
        numBandwidthLimit = AddLabeledNumeric(flExp, "Bandwidth limit (kbps):", 0, 1000000, 0, "Omezení rychlosti odesílání v kbps (0 = bez limitu).");
        var btnNet = new Button { Text = "Zobrazit síťové adaptéry", Width = 180, Height = 32, Margin = new Padding(0, 16, 0, 0) };
        btnNet.Click += (s, e) => MessageBox.Show(NetworkAdapterInfo.GetHelpText(), "Síťové adaptéry", MessageBoxButtons.OK, MessageBoxIcon.Information);
        toolTip.SetToolTip(btnNet, "Zobrazí síťové adaptéry počítače a informace potřebné pro výběr Source IP a IP rotaci.");
        flExp.Controls.Add(btnNet);
        tabExp.Controls.Add(flExp);
        tabs.TabPages.Add(tabExp);

        // --- Tab: Tempo a ochrana ---
        var tabPace = new TabPage("Tempo a ochrana") { Padding = new Padding(8) };
        var flPace = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        var lblPreset = new Label { Text = "Předvolba poskytovatele:", AutoSize = true, Margin = new Padding(0, 4, 0, 2) };
        flPace.Controls.Add(lblPreset);
        cmbProviderPreset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
        foreach (var p in ProviderPresets.All)
            cmbProviderPreset.Items.Add(p.DisplayName);
        cmbProviderPreset.SelectedIndex = 2; // Standard
        cmbProviderPreset.SelectedIndexChanged += (s, e) => ApplyProviderPreset();
        toolTip.SetToolTip(lblPreset, "Předvolba upraví několik parametrů tempa a ochrany podle typu cílového SMTP serveru.");
        toolTip.SetToolTip(cmbProviderPreset, "Vybere doporučené tempo a ochranu pro daný typ serveru. Poté můžete cokoliv upravit ručně.");
        flPace.Controls.Add(cmbProviderPreset);

        chkJitter = AddCheckBox(flPace, "Zapnout jitter (± %)", true, "Náhodná odchylka intervalu slouží k modelování nepravidelného provozu v autorizovaném testu. Neobchází antispamové filtry a nezaručuje doručení do Inboxu.");
        numJitterPercent = AddLabeledNumeric(flPace, "Jitter (± %):", 0, 50, 15, "15 % při 1000 ms = 850–1150 ms.");
        chkBurst = AddCheckBox(flPace, "Burst + pause", false, "Po zadaném počtu zpráv vloží delší pauzu. Slouží k simulaci dávkových scénářů a ověřování limitů serveru.");
        numBurstSize = AddLabeledNumeric(flPace, "Velikost burstu:", 1, 100, 10, "Počet zpráv v jedné dávce.");
        numBurstPause = AddLabeledNumeric(flPace, "Pauza po burstu (s):", 1, 3600, 30, "Délka pauzy po dokončení burstu.");
        chkBackoff = AddCheckBox(flPace, "Progressive backoff", true, "Po chybách 4xx/5xx nebo po N úspěšných zprávách zpomalovat.");
        numBackoffAfter = AddLabeledNumeric(flPace, "Úspěchů do zpomalení:", 1, 10000, 50, "Po kolika úspěšných zprávách se interval prodlouží.");
        numBackoffMult = AddLabeledNumeric(flPace, "Násobitel intervalu (×10):", 11, 50, 15, "1.5 = hodnota 15. Interval se vynásobí tímto číslem.");
        numMaxInterval = AddLabeledNumeric(flPace, "Max. interval (ms):", 100, 3600000, 60000, "Strop, kam až může interval narůst.");
        chkPerRecipient = AddCheckBox(flPace, "Limit na příjemce", false, "Max. X zpráv na jednu adresu za časové okno.");
        numMaxPerRecipient = AddLabeledNumeric(flPace, "Max. zpráv / příjemce:", 1, 10000, 20, "");
        numPerRecipientWindow = AddLabeledNumeric(flPace, "Okno (minuty):", 1, 10080, 60, "");
        chkTimeWindow = AddCheckBox(flPace, "Omezit na denní dobu", false, "Odesílat jen v zadaném intervalu hodin.");
        numWindowFrom = AddLabeledNumeric(flPace, "Od hodiny:", 0, 23, 8, "");
        numWindowTo = AddLabeledNumeric(flPace, "Do hodiny:", 0, 23, 18, "");
        chkWarmup = AddCheckBox(flPace, "Warm-up plán", false, "Postupně zvyšuje tempo řízeného testu. Neobchází pravidla poskytovatele ani nezaručuje doručení do Inboxu.");
        txtWarmupPhases = AddLabeledText(flPace, "Fáze (zprávy; oddělené):", "Např. 50;150;500 – počet zpráv v jednotlivých fázích.");
        txtWarmupPhases.Text = "50;150;500";
        chkGreylist = AddCheckBox(flPace, "Detekovat greylist", true, "451/452/try again – odložený retry.");
        numGreylistMinutes = AddLabeledNumeric(flPace, "Greylist retry (min):", 1, 120, 10, "");
        numGreylistRetries = AddLabeledNumeric(flPace, "Max. greylist retry:", 0, 20, 3, "");
        chkRbl = AddCheckBox(flPace, "Zkontrolovat RBL před startem", false, "Spamhaus Zen – pouze varování, nestopuje test.");
        chkCollectObserved = AddCheckBox(flPace, "Sbírat pozorované SMTP odpovědi", true, "Živá tabulka kódů a doporučení.");

        lblPaceStatus = new Label { Text = "Stav tempa: —", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 12, 0, 2) };
        flPace.Controls.Add(lblPaceStatus);
        lblEffectiveInterval = new Label { Text = "Efektivní interval: —", AutoSize = true, Margin = new Padding(0, 2, 0, 8) };
        flPace.Controls.Add(lblEffectiveInterval);

        btnCheckRbl = new Button { Text = "Zkontrolovat source IP na RBL", Width = 220, Height = 28, Margin = new Padding(0, 4, 0, 4) };
        btnCheckRbl.Click += async (s, e) => await OnCheckRblAsync();
        flPace.Controls.Add(btnCheckRbl);
        lblRblResult = new Label { Text = "", AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 2, 0, 8) };
        flPace.Controls.Add(lblRblResult);

        var lblObs = new Label { Text = "Pozorované SMTP odpovědi (během testu):", AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        flPace.Controls.Add(lblObs);
        gridObserved = new DataGridView
        {
            Width = 520, Height = 120, AllowUserToAddRows = false, ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false
        };
        gridObserved.Columns.Add("Code", "Kód");
        gridObserved.Columns.Add("Text", "Text");
        gridObserved.Columns.Add("Count", "Počet");
        gridObserved.Columns.Add("Class", "Klasifikace");
        gridObserved.Columns.Add("Action", "Doporučení");
        toolTip.SetToolTip(lblObs, "Tabulka průběžně zaznamenává odpovědi SMTP serveru, jejich četnost a doporučenou reakci.");
        toolTip.SetToolTip(gridObserved, "Přehled odpovědí serveru během testu. Kódy 2xx obvykle znamenají úspěch, 4xx dočasný problém a 5xx trvalé odmítnutí.");
        flPace.Controls.Add(gridObserved);

        var lblKnow = new Label { Text = "Znalostní báze filtrů a limitů:", AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
        flPace.Controls.Add(lblKnow);
        gridKnowledge = new DataGridView
        {
            Width = 520, Height = 140, AllowUserToAddRows = false, ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false
        };
        gridKnowledge.Columns.Add("Provider", "Poskytovatel");
        gridKnowledge.Columns.Add("Limit", "Typický limit");
        gridKnowledge.Columns.Add("Response", "Reakce");
        gridKnowledge.Columns.Add("Reco", "Doporučení");
        foreach (var e in FilterKnowledgeBase.DefaultEntries)
            gridKnowledge.Rows.Add(e.Provider, e.TypicalLimit, e.TypicalResponse, e.Recommendation);
        toolTip.SetToolTip(lblKnow, "Orientační znalostní báze typických limitů a reakcí vybraných poskytovatelů. Nejde o garantované aktuální limity.");
        toolTip.SetToolTip(gridKnowledge, "Orientační přehled limitů a doporučení. Skutečné limity se mohou lišit podle účtu, reputace IP a aktuální politiky poskytovatele.");
        flPace.Controls.Add(gridKnowledge);

        tabPace.Controls.Add(flPace);
        tabs.TabPages.Add(tabPace);

        // Záložka rychlého SMTP logu (DataGrid, max 1000 řádků, thread-safe)
        var tabSmtpLog = new TabPage("SMTP log") { Padding = new Padding(4) };
        smtpLogTab = new SmtpLogTab();
        toolTip.SetToolTip(smtpLogTab, "Živý přehled SMTP výsledků. Zobrazuje nejvýše 1 000 posledních řádků; čekající fronta je také omezená. Nejde o trvalý diskový log.");
        tabSmtpLog.Controls.Add(smtpLogTab);
        tabs.TabPages.Add(tabSmtpLog);
        InitializeMailboxMonitorTab();

        // === Bottom area ===
        var bottomPanel = new Panel { Dock = DockStyle.Fill };
        // Resizable live workspace: drag the splitter to give more room to the
        // application log or to the delivery pipeline / worker status area.
        bottomSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1280, 600),
            Orientation = Orientation.Vertical,
            SplitterDistance = 760,
            SplitterWidth = 8,
            Panel1MinSize = 400,
            Panel2MinSize = 300,
            BackColor = Color.FromArgb(220, 224, 230)
        };

        rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGray,
            Font = new Font("Consolas", 10F), BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };
        toolTip.SetToolTip(rtbLog, "Průběžný aplikační log pouze v paměti. Při novém běhu se vymaže a při delším běhu se automaticky zkracuje na posledních přibližně 800 tisíc znaků; nevytváří samostatný diskový soubor.");
        bottomSplit.Panel1.Controls.Add(rtbLog);

        var rightPanel = new Panel { Dock = DockStyle.Fill };

        // Cesta odesílání – pipeline od PC k cílovému serveru (začíná zašedlá)
        var lblPath = new Label
        {
            Text = "Cesta odesílání (PC → cílový server):",
            AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), Dock = DockStyle.Top
        };
        rightPanel.Controls.Add(lblPath);
        pnlDeliveryPath = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 52, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, AutoScroll = true, BackColor = Color.FromArgb(250, 250, 250),
            Padding = new Padding(4)
        };
        BuildDeliveryPathPipeline();
        rightPanel.Controls.Add(pnlDeliveryPath);

        var lblWorkers = new Label { Text = "Workery:", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), Dock = DockStyle.Top };
        pnlWorkers = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 100, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, BackColor = Color.FromArgb(245, 245, 245) };
        rightPanel.Controls.Add(pnlWorkers);
        rightPanel.Controls.Add(lblWorkers);

        var lblCheck = new Label { Text = "Stav zprávy:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 4) };
        rightPanel.Controls.Add(lblCheck);
        var pnlCheck = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        for (int i = 0; i < 6; i++)
        {
            _checklistLabels[i] = new Label
            {
                Text = _checklistTexts[i], AutoSize = true,
                Padding = new Padding(6, 3, 6, 3), Margin = new Padding(2),
                BackColor = Color.LightGray, ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 9.5f), BorderStyle = BorderStyle.FixedSingle
            };
            pnlCheck.Controls.Add(_checklistLabels[i]);
            if (i < 5)
                pnlCheck.Controls.Add(new Label { Text = "→", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(0, 3, 0, 3) });
        }
        rightPanel.Controls.Add(pnlCheck);

        bottomSplit.Panel2.Controls.Add(rightPanel);
        bottomPanel.Controls.Add(bottomSplit);

        // Resize the configuration tabs and the live workspace independently.
        workspaceSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(1280, 800),
            Orientation = Orientation.Horizontal,
            SplitterDistance = 480,
            SplitterWidth = 8,
            Panel1MinSize = 260,
            Panel2MinSize = 180,
            BackColor = Color.FromArgb(220, 224, 230)
        };
        workspaceSplit.Panel1.Controls.Add(tabs);
        workspaceSplit.Panel2.Controls.Add(bottomPanel);
        Controls.Add(workspaceSplit);

        // === StatusStrip ===
        statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        progressBar = new ToolStripProgressBar { Width = 200, Maximum = 100 };
        lblStatus = new ToolStripStatusLabel { Text = "Připraveno", Spring = true };
        lblEta = new ToolStripStatusLabel { Text = "ETA: —" };
        lblSentFailed = new ToolStripStatusLabel { Text = "OK:0 FAIL:0" };
        lblCapacityRun = new ToolStripStatusLabel { Text = string.Empty };
        statusStrip.Items.Add(lblStatus);
        statusStrip.Items.Add(progressBar);
        statusStrip.Items.Add(lblEta);
        statusStrip.Items.Add(lblSentFailed);
        statusStrip.Items.Add(lblCapacityRun);
        Controls.Add(statusStrip);

        // Give controls that are created outside the labeled helpers a useful fallback hint.
        ApplyFallbackToolTips(this);
    }

    private static string LayoutPreferencesFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MailLoadTester", "ui-layout.json");

    private sealed record UiLayoutPreferences(int WorkspaceSplitterDistance, int BottomSplitterDistance);

    private void LoadLayoutPreferences()
    {
        try
        {
            var path = LayoutPreferencesFile;
            if (!File.Exists(path))
                return;

            var preferences = System.Text.Json.JsonSerializer.Deserialize<UiLayoutPreferences>(File.ReadAllText(path));
            if (preferences is null)
                return;

            if (workspaceSplit.Width > 0 && workspaceSplit.Height > 0)
            {
                var max = Math.Max(workspaceSplit.Panel1MinSize,
                    workspaceSplit.Height - workspaceSplit.Panel2MinSize - workspaceSplit.SplitterWidth);
                workspaceSplit.SplitterDistance = Math.Clamp(
                    preferences.WorkspaceSplitterDistance, workspaceSplit.Panel1MinSize, max);
            }

            if (bottomSplit.Width > 0)
            {
                var max = Math.Max(bottomSplit.Panel1MinSize,
                    bottomSplit.Width - bottomSplit.Panel2MinSize - bottomSplit.SplitterWidth);
                bottomSplit.SplitterDistance = Math.Clamp(
                    preferences.BottomSplitterDistance, bottomSplit.Panel1MinSize, max);
            }
        }
        catch
        {
            // A damaged or inaccessible optional UI preference must never prevent startup.
        }
    }

    private void SaveLayoutPreferences()
    {
        try
        {
            var path = LayoutPreferencesFile;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var preferences = new UiLayoutPreferences(
                workspaceSplit.SplitterDistance,
                bottomSplit.SplitterDistance);
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(preferences));
        }
        catch
        {
            // Layout persistence is best-effort and must not block application shutdown.
        }
    }

    private TextBox AddLabeledText(Control parent, string label, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var txt = new TextBox { Width = 360, Margin = new Padding(0, 0, 0, 4) };
        parent.Controls.Add(txt);
        SetFieldToolTip(lbl, txt, tip);
        return txt;
    }

    private NumericUpDown AddLabeledNumeric(Control parent, string label, decimal min, decimal max, decimal value, string tip, int decimalPlaces = 0)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var num = new NumericUpDown { Minimum = min, Maximum = max, Value = value, DecimalPlaces = decimalPlaces, Increment = decimalPlaces > 0 ? 0.1m : 1m, Width = 120, Margin = new Padding(0, 0, 0, 4) };
        parent.Controls.Add(num);
        var rangeTip = $"{tip}{(string.IsNullOrWhiteSpace(tip) ? string.Empty : Environment.NewLine)}Rozsah: {min} až {max}. Výchozí hodnota: {value}.";
        SetFieldToolTip(lbl, num, rangeTip);
        return num;
    }

    private ComboBox AddLabeledCombo(Control parent, string label, string[] items, string selected, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Margin = new Padding(0, 0, 0, 4) };
        cmb.Items.AddRange(items);
        cmb.SelectedItem = selected;
        parent.Controls.Add(cmb);
        SetFieldToolTip(lbl, cmb, tip);
        return cmb;
    }

    private CheckBox AddCheckBox(Control parent, string text, bool check, string tip)
    {
        var chk = new CheckBox { Text = text, AutoSize = true, Checked = check, Margin = new Padding(0, 6, 0, 2) };
        parent.Controls.Add(chk);
        toolTip.SetToolTip(chk, tip);
        return chk;
    }

    private void SetFieldToolTip(Control label, Control field, string tip)
    {
        if (string.IsNullOrWhiteSpace(tip))
            tip = $"Nastavení „{label.Text.TrimEnd(':')}“. Podrobnosti závisí na zvoleném serveru a konfiguraci testu.";
        toolTip.SetToolTip(label, tip);
        toolTip.SetToolTip(field, tip);
    }

    private void ApplyFallbackToolTips(Control parent)
    {
        for (var i = 0; i < parent.Controls.Count; i++)
        {
            var child = parent.Controls[i];
            if (string.IsNullOrWhiteSpace(toolTip.GetToolTip(child)))
            {
                var context = parent.Controls
                    .Cast<Control>()
                    .Take(i)
                    .OfType<Label>()
                    .LastOrDefault()?.Text.Trim().TrimEnd(':');
                if (string.IsNullOrWhiteSpace(context) && parent is GroupBox groupBox)
                    context = groupBox.Text;

                if (child is Button button)
                    toolTip.SetToolTip(button, $"Akce: {button.Text.Trim().Trim('…', '.')}. Kliknutím tuto funkci spustíš.");
                else if (child is TextBox)
                    toolTip.SetToolTip(child, $"Textové pole{(string.IsNullOrWhiteSpace(context) ? string.Empty : $" pro {context}")}. Zadej požadovanou hodnotu.");
                else if (child is NumericUpDown numeric)
                    toolTip.SetToolTip(numeric, $"Číselné nastavení{(string.IsNullOrWhiteSpace(context) ? string.Empty : $" – {context}")}. Rozsah: {numeric.Minimum} až {numeric.Maximum}. Aktuální hodnota: {numeric.Value}.");
                else if (child is ComboBox)
                    toolTip.SetToolTip(child, $"Výběr možnosti{(string.IsNullOrWhiteSpace(context) ? string.Empty : $" – {context}")}. Vyber jednu z nabízených hodnot.");
                else if (child is ListBox)
                    toolTip.SetToolTip(child, $"Seznam položek{(string.IsNullOrWhiteSpace(context) ? string.Empty : $" – {context}")}. Vyber položku pro další práci.");
                else if (child is GroupBox group)
                    toolTip.SetToolTip(group, $"Skupina souvisejících nastavení: {group.Text}.");
            }

            if (child.HasChildren)
                ApplyFallbackToolTips(child);
        }
    }

    private void WireEvents()
    {
        btnStart.Click += async (s, e) => await OnStartAsync();
        btnStop.Click += (s, e) => OnStop();
        btnSaveProfile.Click += async (s, e) => await OnSaveProfileAsync();
        btnLoadProfile.Click += async (s, e) => await OnLoadProfileAsync();
        btnExportReport.Click += async (s, e) => await OnExportReportAsync();
        btnWizard.Click += (s, e) => ShowGettingStartedWizard();
        chkDarkMode.CheckedChanged += (s, e) => ApplyTheme(chkDarkMode.Checked);
        chkTestMode.CheckedChanged += (s, e) => UpdateSafetyBanner();
        btnTestConnection.Click += async (s, e) => await OnTestConnectionAsync();
        btnSendTest.Click += async (s, e) => await OnSendTestMessageAsync();
        numMaxRandomAttachments.ValueChanged += (s, e) => UpdateRandomAttachmentSafetyInfo();
        numRandomAttachmentSizeMb.ValueChanged += (s, e) => UpdateRandomAttachmentSafetyInfo();
        numConcurrency.ValueChanged += (s, e) => UpdateRandomAttachmentSafetyInfo();
        btnEmlBrowse.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog { Filter = "EML files|*.eml|All files|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK) txtEmlTemplate.Text = dlg.FileName;
        };
        WireMailboxMonitorEvents();
    }

    private void SetDefaults()
    {
        txtHost.Text = "smtp.example.com";
        txtFrom.Text = "test@example.com";
        txtTo.Text = "user@example.com";
        txtSubject.Text = "MailLoadTester test";
        txtBody.Text = "Toto je testovací zpráva generovaná aplikací MailLoadTester.";
        txtDisplayName.Text = "MailLoadTester";
        txtProxyHost.Text = "127.0.0.1";
        txtSessionLogPath.Text = Path.Combine(AppContext.BaseDirectory, "smtp-session.log");
        UpdateRandomAttachmentSafetyInfo();
    }

    private void UpdateRandomAttachmentSafetyInfo()
    {
        if (lblRandomAttachmentSafety is null || numRandomAttachmentSizeMb is null || numMaxRandomAttachments is null || numConcurrency is null) return;
        var estimate = AttachmentPlanner.EstimateRandomAttachments(
            (int)numRandomAttachmentSizeMb.Value,
            (int)numMaxRandomAttachments.Value,
            (int)numConcurrency.Value);
        var status = estimate.IsSafe ? "✓ Bezpečné" : "⚠ BLOKOVÁNO – před startem se RAM nebude alokovat";
        lblRandomAttachmentSafety.Text = $"{status}\r\n{estimate.Explanation}\r\nDostupná RAM: {AttachmentPlanner.FormatBytes(estimate.AvailableBytes)}";
        lblRandomAttachmentSafety.ForeColor = estimate.IsSafe ? Color.DarkGreen : Color.DarkRed;
    }
}
