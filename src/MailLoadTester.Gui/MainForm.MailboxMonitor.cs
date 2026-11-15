using MailLoadTester;

namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private void InitializeMailboxMonitorTab()
    {
        var tab = new TabPage("Mailbox Monitor") { Padding = new Padding(12) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));

        var connectionGroup = new GroupBox { Text = "IMAP přístup (volitelné)", Dock = DockStyle.Fill };
        var connectionFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        txtMailboxHost = AddLabeledText(connectionFlow, "IMAP host:", "Adresa IMAP serveru, například imap.example.com. Bez IMAP údajů SMTP test funguje dál, pouze nebude možné ověřit stav schránky.");
        numMailboxPort = AddLabeledNumeric(connectionFlow, "IMAP port:", 1, 65535, 993, "993 = implicit TLS, 143 = obvykle STARTTLS.");
        cmbMailboxSecurity = AddLabeledCombo(connectionFlow, "Zabezpečení:", ["ImplicitTls", "StartTls"], "ImplicitTls", "ImplicitTls naváže TLS hned při připojení (typicky port 993). StartTls naváže TLS po úvodním IMAP pozdravu (typicky port 143).");
        txtMailboxUsername = AddLabeledText(connectionFlow, "Uživatelské jméno:", "Účet, ke kterému máš oprávnění přistupovat přes IMAP. Často celá e-mailová adresa.");
        txtMailboxPassword = AddLabeledText(connectionFlow, "Heslo / app-password:", "IMAP heslo nebo aplikační heslo. Zůstává pouze v paměti aplikace a neukládá se do profilu, logu ani reportu.");
        txtMailboxPassword.PasswordChar = '●';

        var actionPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 4) };
        btnMailboxRefresh = new Button { Text = "Test / Obnovit", Width = 120, Height = 32 };
        btnMailboxClear = new Button { Text = "Vymazat údaje", Width = 120, Height = 32 };
        actionPanel.Controls.Add(btnMailboxRefresh);
        actionPanel.Controls.Add(btnMailboxClear);
        connectionFlow.Controls.Add(actionPanel);

        chkMailboxStopAtQuota = AddCheckBox(connectionFlow, "Zastavit SMTP test při dosažení kvóty", false, "Volitelně zastaví právě běžící SMTP test, až když IMAP server poskytne skutečnou STORAGE kvótu a její využití dosáhne nastaveného limitu. Bez dostupné kvóty se automatické zastavení neaktivuje. Kontrola probíhá po 30 sekundách, takže limit může být mezi kontrolami překročen.");
        numMailboxStopPercent = AddLabeledNumeric(connectionFlow, "Limit zaplnění (%):", 1, 100, 80, "Automatické zastavení při dosažení této hodnoty skutečné IMAP STORAGE kvóty. Výchozí hodnota je 80 %.");
        connectionGroup.Controls.Add(connectionFlow);
        layout.Controls.Add(connectionGroup, 0, 0);

        var summaryGroup = new GroupBox { Text = "Stav schránky", Dock = DockStyle.Fill };
        var summaryFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(14)
        };
        lblMailboxPercent = new Label
        {
            Text = "—",
            AutoSize = true,
            Font = new Font("Segoe UI", 28F, FontStyle.Bold),
            Margin = new Padding(0, 2, 0, 4)
        };
        summaryFlow.Controls.Add(lblMailboxPercent);
        progressMailboxQuota = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Width = 480,
            Height = 24,
            Style = ProgressBarStyle.Continuous
        };
        summaryFlow.Controls.Add(progressMailboxQuota);

        lblMailboxMessages = CreateMailboxMetricLabel("Uložené zprávy: —");
        lblMailboxUnread = CreateMailboxMetricLabel("Nepřečtené v INBOX: —");
        lblMailboxUsage = CreateMailboxMetricLabel("Obsazeno (kvóta): —");
        lblMailboxQuota = CreateMailboxMetricLabel("Kvóta: —");
        lblMailboxInboxSize = CreateMailboxMetricLabel("Velikost INBOX: —");
        lblMailboxLastRefresh = CreateMailboxMetricLabel("Poslední kontrola: —");
        summaryFlow.Controls.Add(lblMailboxMessages);
        summaryFlow.Controls.Add(lblMailboxUnread);
        summaryFlow.Controls.Add(lblMailboxUsage);
        summaryFlow.Controls.Add(lblMailboxQuota);
        summaryFlow.Controls.Add(lblMailboxInboxSize);
        summaryFlow.Controls.Add(lblMailboxLastRefresh);

        lblMailboxStatus = new Label
        {
            Text = "IMAP monitor je volitelný. Bez přihlašovacích údajů může SMTP test běžet naslepo; skutečné zaplnění schránky ale nebude ověřeno.",
            AutoSize = true,
            MaximumSize = new Size(540, 0),
            Margin = new Padding(0, 12, 0, 4),
            ForeColor = Color.DimGray
        };
        summaryFlow.Controls.Add(lblMailboxStatus);
        summaryGroup.Controls.Add(summaryFlow);
        layout.Controls.Add(summaryGroup, 1, 0);

        toolTip.SetToolTip(lblMailboxPercent, "Procento se zobrazí pouze tehdy, když IMAP server vrátí STORAGE kvótu s kladným limitem. Není-li kvóta dostupná, aplikace procento nevymýšlí.");
        toolTip.SetToolTip(progressMailboxQuota, "Průběh skutečného využití IMAP STORAGE kvóty. Velikost samotné složky INBOX není totéž co celková kvóta účtu.");
        toolTip.SetToolTip(lblMailboxStatus, "SMTP odpověď 250 OK znamená přijetí zprávy serverem, nikoliv potvrzení jejího uložení do cílové schránky. Skutečné uložení lze ověřit pouze nezávislou kontrolou schránky, například přes IMAP.");
        toolTip.SetToolTip(btnMailboxRefresh, "Naváže IMAP spojení, ověří přihlášení a načte dostupnou kvótu a údaje složky INBOX. Nic ve schránce nemění.");
        toolTip.SetToolTip(btnMailboxClear, "Vymaže IMAP host, uživatelské jméno i heslo z formuláře.");

        tab.Controls.Add(layout);
        tabs.TabPages.Add(tab);
    }

    private static Label CreateMailboxMetricLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 10F),
        Margin = new Padding(0, 7, 0, 2)
    };

    private void WireMailboxMonitorEvents()
    {
        btnMailboxRefresh.Click += async (_, _) =>
        {
            await RefreshMailboxMonitorAsync();
            if (_isRunning && IsMailboxMonitorConfigured())
                EnsureMailboxMonitorTimerStarted();
        };
        btnMailboxClear.Click += (_, _) => ClearMailboxMonitorCredentials();
    }

    private void ClearMailboxMonitorCredentials()
    {
        StopMailboxMonitorPolling();
        _mailboxMonitorCts?.Cancel();
        chkMailboxStopAtQuota.Checked = false;
        txtMailboxHost.Clear();
        txtMailboxUsername.Clear();
        txtMailboxPassword.Clear();
        lblMailboxStatus.Text = "IMAP údaje byly vymazány. SMTP test může běžet dál, ale stav schránky nebude ověřován.";
        lblMailboxPercent.Text = "—";
        progressMailboxQuota.Value = 0;
        lblMailboxMessages.Text = "Uložené zprávy: —";
        lblMailboxUnread.Text = "Nepřečtené v INBOX: —";
        lblMailboxUsage.Text = "Obsazeno (kvóta): —";
        lblMailboxQuota.Text = "Kvóta: —";
        lblMailboxInboxSize.Text = "Velikost INBOX: —";
        lblMailboxLastRefresh.Text = "Poslední kontrola: —";
    }

    private bool IsMailboxMonitorConfigured()
        => !string.IsNullOrWhiteSpace(txtMailboxHost.Text)
           && !string.IsNullOrWhiteSpace(txtMailboxUsername.Text)
           && !string.IsNullOrEmpty(txtMailboxPassword.Text);

    private void StartMailboxMonitorPolling()
    {
        if (!IsMailboxMonitorConfigured())
        {
            if (chkMailboxStopAtQuota.Checked)
                lblMailboxStatus.Text = "Pozor: automatické zastavení podle kvóty není aktivní, protože nejsou vyplněné IMAP údaje. SMTP test poběží bez ověření zaplnění.";
            return;
        }

        EnsureMailboxMonitorTimerStarted();
        _ = RefreshMailboxMonitorAsync();
    }

    private void EnsureMailboxMonitorTimerStarted()
    {
        _mailboxMonitorTimer ??= new System.Windows.Forms.Timer();
        _mailboxMonitorTimer.Interval = 30_000;
        _mailboxMonitorTimer.Tick -= MailboxMonitorTimer_Tick;
        _mailboxMonitorTimer.Tick += MailboxMonitorTimer_Tick;
        _mailboxMonitorTimer.Start();
    }

    private async void MailboxMonitorTimer_Tick(object? sender, EventArgs e)
        => await RefreshMailboxMonitorAsync();

    private void StopMailboxMonitorPolling()
        => _mailboxMonitorTimer?.Stop();

    private async Task RefreshMailboxMonitorAsync()
    {
        if (_mailboxRefreshInProgress || IsDisposed || _closeApproved)
            return;

        if (!IsMailboxMonitorConfigured())
        {
            lblMailboxStatus.Text = "Doplň IMAP host, uživatelské jméno a heslo. Bez nich SMTP test funguje dál, ale zaplnění schránky nelze ověřit.";
            return;
        }

        MailboxMonitorOptions options;
        try
        {
            var security = string.Equals(cmbMailboxSecurity.SelectedItem?.ToString(), "StartTls", StringComparison.OrdinalIgnoreCase)
                ? MailboxMonitorSecurity.StartTls
                : MailboxMonitorSecurity.ImplicitTls;
            options = new MailboxMonitorOptions(
                txtMailboxHost.Text.Trim(),
                (int)numMailboxPort.Value,
                security,
                txtMailboxUsername.Text.Trim(),
                txtMailboxPassword.Text);
            MailboxMonitorService.ValidateOptions(options);
        }
        catch (Exception ex)
        {
            lblMailboxStatus.Text = "Neplatná konfigurace IMAP: " + ex.Message;
            return;
        }

        _mailboxRefreshInProgress = true;
        btnMailboxRefresh.Enabled = false;
        using var cts = new CancellationTokenSource();
        _mailboxMonitorCts = cts;
        _mailboxMonitorCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            lblMailboxStatus.Text = "Připojuji se k IMAP serveru a načítám stav schránky…";
            var snapshot = await new MailboxMonitorService().ReadAsync(options, cts.Token);
            if (IsDisposed || _closeApproved)
                return;

            RenderMailboxMonitorSnapshot(snapshot);
            if (_isRunning && chkMailboxStopAtQuota.Checked
                && MailboxMonitorMath.ShouldStop(snapshot.QuotaUsagePercent, (int)numMailboxStopPercent.Value)
                && _cts is { IsCancellationRequested: false })
            {
                _mailboxQuotaStopTriggered = true;
                lblMailboxStatus.Text += " Dosažen nastavený limit; SMTP test zastavuji.";
                AppendLog($"Mailbox Monitor: využití kvóty {snapshot.QuotaUsagePercent:F1} % dosáhlo limitu {(int)numMailboxStopPercent.Value} %. Zastavuji SMTP test.", Color.OrangeRed);
                _cts.Cancel();
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Expected when the form is closing.
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !_closeApproved)
            {
                lblMailboxStatus.Text = "IMAP kontrola selhala: " + ex.Message;
                lblMailboxStatus.ForeColor = Color.DarkRed;
            }
        }
        finally
        {
            _mailboxMonitorCts = null;
            _mailboxMonitorCompletion?.TrySetResult(true);
            _mailboxMonitorCompletion = null;
            _mailboxRefreshInProgress = false;
            if (!IsDisposed && !_closeApproved)
                btnMailboxRefresh.Enabled = true;
        }
    }

    private void RenderMailboxMonitorSnapshot(MailboxMonitorSnapshot snapshot)
    {
        var percent = snapshot.QuotaUsagePercent;
        if (percent.HasValue)
        {
            lblMailboxPercent.Text = $"{percent.Value:F1} %";
            progressMailboxQuota.Value = (int)Math.Clamp(Math.Round(percent.Value), 0, 100);
            lblMailboxUsage.Text = "Obsazeno (kvóta): " + FormatMailboxBytes(snapshot.QuotaUsedBytes);
            lblMailboxQuota.Text = "Kvóta: " + FormatMailboxBytes(snapshot.QuotaLimitBytes);
        }
        else
        {
            lblMailboxPercent.Text = "—";
            progressMailboxQuota.Value = 0;
            lblMailboxUsage.Text = snapshot.QuotaUsedBytes.HasValue
                ? "Využití (bez dostupného limitu): " + FormatMailboxBytes(snapshot.QuotaUsedBytes)
                : "Obsazeno (kvóta): nedostupné";
            lblMailboxQuota.Text = "Kvóta: server ji neposkytuje nebo nemá kladný limit";
        }

        var messageCount = snapshot.QuotaMessageCount.HasValue
            ? $"Zprávy v kvótě: {snapshot.QuotaMessageCount.Value:N0}"
            : snapshot.InboxMessageCount.HasValue
                ? $"Zprávy v INBOX: {snapshot.InboxMessageCount.Value:N0}"
                : "Zprávy: nedostupné";
        lblMailboxMessages.Text = messageCount;
        lblMailboxUnread.Text = snapshot.InboxUnreadCount.HasValue
            ? $"Nepřečtené v INBOX: {snapshot.InboxUnreadCount.Value:N0}"
            : "Nepřečtené v INBOX: nedostupné";
        lblMailboxInboxSize.Text = "Velikost INBOX (nikoli celé kvóty): " + FormatMailboxBytes(snapshot.InboxSizeBytes);
        lblMailboxLastRefresh.Text = "Poslední kontrola: " + snapshot.CheckedAt.ToLocalTime().ToString("g");
        lblMailboxStatus.Text = snapshot.Status;
        lblMailboxStatus.ForeColor = percent.HasValue ? Color.DarkGreen : Color.DarkOrange;
    }

    private static string FormatMailboxBytes(long? bytes)
    {
        if (!bytes.HasValue)
            return "nedostupné";

        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = (double)bytes.Value;
        var unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }
        return $"{value:N2} {units[unit]}";
    }
}