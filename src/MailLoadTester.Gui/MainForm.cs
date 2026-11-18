namespace MailLoadTester.Gui;

public sealed partial class MainForm : Form
{
    // === Controls ===
    private Button btnStart = null!, btnStop = null!, btnSaveProfile = null!, btnLoadProfile = null!;
    private TabControl tabs = null!;
    private TableLayoutPanel _smtpLayout = null!;
    private CheckBox chkRememberSmtpPassword = null!;
    private SplitContainer workspaceSplit = null!, bottomSplit = null!;
    private ToolTip toolTip = null!;

    // UI enhancement
    private Panel pnlSafetyBanner = null!;
    private Label lblSafetyBanner = null!;
    private Button btnWizard = null!;
    private CheckBox chkDarkMode = null!;
    private bool _darkMode;
    private bool _loadingSmtpCredentials;

    // SMTP tab
    private ComboBox txtHost = null!;
    private NumericUpDown numPort = null!;
    private ComboBox cmbSecurity = null!;
    private CheckBox chkIgnoreCert = null!;
    private CheckBox chkAuth = null!;
    private ComboBox cmbAuthMethod = null!;
    private ComboBox txtUser = null!;
    private TextBox txtPass = null!;
    private Button btnTestConnection = null!, btnSendTest = null!;
    private Button btnGoogleSignIn = null!, btnGoogleSignOut = null!, btnSendGoogleTest = null!;
    private Label lblGoogleAccountStatus = null!;
    private ToolStripStatusLabel lblTorPersistentStatus = null!;
    private ComboBox txtSourceIp = null!;
    private ComboBox cmbLocalIp = null!;
    private TextBox txtIpv6Prefix = null!;
    private NumericUpDown numIpv6PrefixLen = null!;
    private TextBox txtIpv4Rotation = null!;
    private CheckBox chkIpv4RotationRandom = null!;
    private TextBox txtClientCert = null!, txtClientCertPass = null!;
    private NumericUpDown numConnectTimeout = null!, numReadTimeout = null!;

    // Message tab
    private ComboBox txtFrom = null!;
    private ComboBox txtTo = null!, txtCc = null!, txtBcc = null!;
    private readonly List<string> _knownEmailAddresses = new();
    private TextBox txtDisplayName = null!, txtSubject = null!, txtBody = null!;
    private CheckBox chkHtmlBody = null!, chkRandomData = null!, chkSmtpUtf8 = null!;
    private CheckBox chkBogusData = null!, chkRandomHtml = null!, chkRandomAttachments = null!, chkVaryMessage = null!;
    private CheckBox chkHideMessageBody = null!, chkHideSenderDisplayName = null!;
    private CheckBox chkRandomizeMessageBody = null!, chkRandomizeSenderDisplayName = null!;
    private NumericUpDown numMaxRandomAttachments = null!, numRandomAttachmentSizeMb = null!;
    private Label lblRandomAttachmentSafety = null!;
    private ListBox lstAttachments = null!, lstInlineAttachments = null!;
    private TextBox txtHeaders = null!;
    private TextBox txtEmlTemplate = null!;
    private Button btnEmlBrowse = null!;

    // Test tab
    private NumericUpDown numCount = null!, numConcurrency = null!, numInterval = null!, numRetries = null!;
    private CheckBox chkCapacityRun = null!, chkCapacityStartupValidation = null!;
    private NumericUpDown numCapacityMaxMessages = null!, numCapacityTargetMiB = null!, numCapacityMaxMessageMiB = null!, numCapacityMaxPerMinute = null!;
    private CheckBox chkBatchMode = null!;
    private NumericUpDown numBatchSize = null!, numBatchPause = null!;
    private CheckBox chkDryRun = null!, chkTestMode = null!;
    private TextBox txtAllowedDomains = null!;

    // Proxy tab
    private CheckBox chkSocks5 = null!;
    private TextBox txtProxyHost = null!;
    private NumericUpDown numProxyPort = null!;
    private TextBox txtProxyUser = null!, txtProxyPass = null!;
    private TextBox txtProxyList = null!;
    private Label lblProxyStatus = null!;
    private FlowLayoutPanel pnlQueueFlow = null!;
    private readonly Dictionary<string, Label> _queueFlowLabels = new(StringComparer.Ordinal);
    // Options without dedicated controls (persist via profile / defaults)
    private int _idleHealthCheckSeconds = 30;
    private int _circuitWindowSize = 100;
    private double _circuitFailurePercent = 90.0;
    private CheckBox chkProxyListRandom = null!;
    private NumericUpDown numProxyBanMinutes = null!;

    // Tempo a ochrana tab
    private ComboBox cmbProviderPreset = null!;
    private CheckBox chkJitter = null!, chkBurst = null!, chkBackoff = null!, chkPerRecipient = null!;
    private CheckBox chkTimeWindow = null!, chkWarmup = null!, chkGreylist = null!, chkRbl = null!, chkCollectObserved = null!;
    private NumericUpDown numJitterPercent = null!, numBurstSize = null!, numBurstPause = null!;
    private NumericUpDown numBackoffAfter = null!, numBackoffMult = null!, numMaxInterval = null!;
    private NumericUpDown numMaxPerRecipient = null!, numPerRecipientWindow = null!;
    private NumericUpDown numWindowFrom = null!, numWindowTo = null!;
    private NumericUpDown numGreylistMinutes = null!, numGreylistRetries = null!;
    private TextBox txtWarmupPhases = null!;
    private Label lblPaceStatus = null!, lblEffectiveInterval = null!;
    private DataGridView gridObserved = null!, gridKnowledge = null!;
    private Button btnCheckRbl = null!;
    private Label lblRblResult = null!;

    // Cesta odesílání (pipeline) – začíná zašedlá, zbarvuje se podle průběhu
    private FlowLayoutPanel pnlDeliveryPath = null!;
    private readonly Dictionary<DeliveryStepKind, Label> _pathLabels = new();
    private readonly DeliveryStepKind[] _pathOrder =
    [
        DeliveryStepKind.DnsMxLookup,
        DeliveryStepKind.TcpConnect,
        DeliveryStepKind.Ehlo,
        DeliveryStepKind.StartTls,
        DeliveryStepKind.Auth,
        DeliveryStepKind.MailFrom,
        DeliveryStepKind.RcptTo,
        DeliveryStepKind.Data,
        DeliveryStepKind.Quit
    ];

    // Advanced tab
    private CheckBox chkDirectMx = null!, chkPreWarm = null!, chkAdaptive = null!, chkCircuit = null!;
    private NumericUpDown numCircuitThreshold = null!;
    private CheckBox chkDashboard = null!;
    private NumericUpDown numDashboardPort = null!;
    private CheckBox chkAutoRestart = null!;
    private NumericUpDown numAutoRestartAttempts = null!;
    private CheckBox chkSessionLog = null!;
    private TextBox txtSessionLogPath = null!;

    // Mailbox Monitor tab (credentials are memory-only and never profile-persisted)
    private TextBox txtMailboxHost = null!, txtMailboxUsername = null!, txtMailboxPassword = null!;
    private NumericUpDown numMailboxPort = null!, numMailboxStopPercent = null!;
    private ComboBox cmbMailboxSecurity = null!;
    private CheckBox chkMailboxStopAtQuota = null!;
    private Button btnMailboxRefresh = null!, btnMailboxClear = null!;
    private ProgressBar progressMailboxQuota = null!;
    private Label lblMailboxPercent = null!, lblMailboxMessages = null!, lblMailboxUnread = null!;
    private Label lblMailboxInboxCount = null!, lblMailboxJunk = null!;
    private Label lblMailboxUsage = null!, lblMailboxQuota = null!, lblMailboxInboxSize = null!;
    private Label lblMailboxStatus = null!, lblMailboxLastRefresh = null!;

    // Síť / integrace tab
    private TextBox txtWebhookUrl = null!;
    private NumericUpDown numBandwidthLimit = null!;
    private ComboBox cmbIpVersion = null!;

    // Bottom panels
    private RichTextBox rtbLog = null!;
    private SmtpLogTab smtpLogTab = null!;
    private FlowLayoutPanel pnlWorkers = null!;
    private Label[]? _workerLabels;
    private Label[] _checklistLabels = new Label[6];
    private readonly string[] _checklistTexts = ["Fronta", "Rate limit", "SMTP", "MIME", "SEND", "OK"];

    // Status
    private StatusStrip statusStrip = null!;
    private ToolStripProgressBar progressBar = null!;
    private ToolStripStatusLabel lblStatus = null!, lblEta = null!, lblSentFailed = null!, lblCapacityRun = null!;
    private Button btnCapacityEstimate = null!;
    private Label lblCapacityEstimate = null!;
    private RunMetricsChart runMetricsChart = null!;
    private AdaptiveControlChart adaptiveControlChart = null!;
    private int _lastRunSmtpSendAttempts;
    private int _lastRunSmtp4xx;
    private int _lastRunSmtp5xx;
    private int _lastRunAccepted;

    // Runtime
    private CancellationTokenSource? _cts;
    private SmtpTestRunner? _runner;
    private bool _isRunning;
    private int _activeRunMessageCount;
    private TaskCompletionSource<bool>? _runCompletion;
    private CancellationTokenSource? _testConnectionCts;
    private TaskCompletionSource<bool>? _testConnectionCompletion;
    private System.Windows.Forms.Timer? _mailboxMonitorTimer;
    private CancellationTokenSource? _mailboxMonitorCts;
    private TaskCompletionSource<bool>? _mailboxMonitorCompletion;
    private bool _mailboxRefreshInProgress;
    private bool _mailboxQuotaStopTriggered;
    private MailboxMonitorSnapshot? _mailboxRunBaseline;
    private MailboxMonitorSnapshot? _mailboxLastSnapshot;
    private bool _closeApproved;
    private DateTime _logThrottleWindow = DateTime.MinValue;
    private DateTime _observedThrottle = DateTime.MinValue;

    public MainForm()
    {
        Text = $"MailLoadTester {MailLoadTester.AppVersion.Current} — SMTP testovací nástroj";
        Size = new Size(1280, 920);
        MinimumSize = new Size(1000, 750);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        InitializeComponent();
        txtUser.TextChanged += (_, _) => SyncFromAddressSuggestions();
        txtFrom.TextChanged += (_, _) => SyncFromAddressSuggestions(addCurrentAddress: true, copyLoginWhenEmpty: false);
        txtTo.TextChanged += (_, _) => RefreshEmailAddressSuggestions();
        txtCc.TextChanged += (_, _) => RefreshEmailAddressSuggestions();
        txtBcc.TextChanged += (_, _) => RefreshEmailAddressSuggestions();
        WireEvents();
        SetDefaults();
        SyncFromAddressSuggestions();
        UpdateSafetyBanner();
        ApplyTheme(false);
        Resize += (_, _) => ApplyResponsiveLayout();
        FormClosing += MainForm_FormClosing;
        Shown += async (_, _) =>
        {
            LoadLayoutPreferences();
            LoadRememberedSmtpCredentials();
            ApplyResponsiveLayout();
            await InitializeGoogleOAuthAsync();
        };
        FormClosed += (_, _) => DisposeGoogleOAuthResources();
        FormClosed += (_, _) => { _torStatusTimer?.Stop(); _torStatusTimer?.Dispose(); _torStatusTimer = null; };
        FormClosed += (_, _) => SaveLayoutPreferences();
    }

    private static bool IsValidEmailSuggestion(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && System.Net.Mail.MailAddress.TryCreate(value.Trim(), out var parsed)
        && parsed.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);

    private void SyncFromAddressSuggestions(bool addCurrentAddress = false, bool copyLoginWhenEmpty = true)
    {
        var login = txtUser.Text.Trim();
        if (IsValidEmailSuggestion(login))
            AddKnownEmailAddress(login);

        foreach (var account in _accountDrafts)
            if (account.Enabled && IsValidEmailSuggestion(account.Username))
                AddKnownEmailAddress(account.Username);

        var current = txtFrom.Text.Trim();
        if (addCurrentAddress && IsValidEmailSuggestion(current))
            AddKnownEmailAddress(current);

        RefreshEmailAddressSuggestions();

        // Most SMTP providers expect the authenticated mailbox to be the sender.
        // Use it as a convenience default, but never overwrite a manually entered From address.
        if (copyLoginWhenEmpty && string.IsNullOrWhiteSpace(txtFrom.Text) && IsValidEmailSuggestion(login))
            txtFrom.Text = login;
    }

    private void AddKnownEmailAddress(string address)
    {
        address = address.Trim();
        if (!IsValidEmailSuggestion(address)) return;
        if (!_knownEmailAddresses.Contains(address, StringComparer.OrdinalIgnoreCase))
            _knownEmailAddresses.Add(address);
    }

    private void RefreshEmailAddressSuggestions()
    {
        if (txtFrom is null) return;

        foreach (var account in _accountDrafts)
            if (account.Enabled && IsValidEmailSuggestion(account.Username))
                AddKnownEmailAddress(account.Username);

        foreach (var address in new[] { txtTo?.Text, txtCc?.Text, txtBcc?.Text })
        {
            if (string.IsNullOrWhiteSpace(address)) continue;
            foreach (var token in address.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (IsValidEmailSuggestion(token))
                    AddKnownEmailAddress(token);
        }

        foreach (var combo in new[] { txtUser, txtFrom, txtTo, txtCc, txtBcc })
        {
            if (combo is null) continue;
            foreach (var address in _knownEmailAddresses)
                if (!combo.Items.Contains(address))
                    combo.Items.Add(address);
        }
    }

    /// <summary>
    /// Closing the window while a test is running used to do nothing special: the
    /// background Task.Run(... RunAsync ...) kept going with no cancellation, and
    /// every subsequent progress/log callback that reached UpdateProgress/AppendLog
    /// would call Invoke() on a Control whose handle was being torn down — throwing
    /// ObjectDisposedException/InvalidOperationException *inside* SmtpTestRunner's
    /// Report() call. Since the per-message send loop's outermost catch treats any
    /// exception as a message failure, this didn't crash the app — it silently made
    /// every remaining message in the run "fail" for an unrelated GUI-teardown
    /// reason, burned through retries, and kept the background loop alive until
    /// MessageCount was exhausted, never actually stopping. Now: request
    /// cancellation, block the close until the run has genuinely wound down, then
    /// let it close.
    /// </summary>
    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closeApproved)
            return;

        var runActive = _isRunning;
        var connectionTestActive = _testConnectionCts != null;
        var mailboxMonitorActive = _mailboxMonitorCts != null;
        var googleOperationActive = _googleOAuthOperationCts != null;
        var googleOperationCompletion = _googleOAuthOperationCompletion;
        if (!runActive && !connectionTestActive && !mailboxMonitorActive && !googleOperationActive)
            return;

        e.Cancel = true;
        lblStatus.Text = "Zavírám — čekám na ukončení běžící operace…";
        _cts?.Cancel();
        _testConnectionCts?.Cancel();
        _mailboxMonitorTimer?.Stop();
        _mailboxMonitorCts?.Cancel();
        _googleOAuthOperationCts?.Cancel();

        if (runActive)
        {
            var completion = _runCompletion;
            if (completion != null)
            {
                try { await completion.Task.ConfigureAwait(true); }
                catch { /* the run's own try/catch in OnStartAsync already handled/reported this */ }
            }
        }

        if (connectionTestActive)
        {
            var completion = _testConnectionCompletion;
            if (completion != null)
            {
                try { await completion.Task.ConfigureAwait(true); }
                catch { /* TestConnection owns its cancellation/error handling. */ }
            }
        }

        if (mailboxMonitorActive)
        {
            var completion = _mailboxMonitorCompletion;
            if (completion != null)
            {
                try { await completion.Task.ConfigureAwait(true); }
                catch { /* Mailbox monitor owns its cancellation/error handling. */ }
            }
        }

        if (googleOperationActive && googleOperationCompletion != null)
        {
            try { await googleOperationCompletion.Task.ConfigureAwait(true); }
            catch { /* Google OAuth operation owns its cancellation/error handling. */ }
        }

        _closeApproved = true;
        if (!IsDisposed)
            Close();
    }

    /// <summary>Shows an error in a persistent, selectable dialog with one-click copy.</summary>
    private void ShowErrorWithCopy(string message, string title, string? technicalDetails = null, string errorCode = GuiErrorCodes.General)
    {
        var copyText = $"Kód chyby: {errorCode}" + Environment.NewLine + message
            + (string.IsNullOrWhiteSpace(technicalDetails)
                ? string.Empty
                : Environment.NewLine + Environment.NewLine + "Technické podrobnosti:" + Environment.NewLine + technicalDetails);

        using var dialog = new Form
        {
            Text = $"{title} [{errorCode}]",
            Width = 720,
            Height = 360,
            MinimumSize = new Size(520, 260),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Font,
            Font = Font
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        var details = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = true,
            Text = copyText
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var close = new Button { Text = "Zavřít", Width = 100, Height = 30, DialogResult = DialogResult.OK };
        var copy = new Button { Text = "Kopírovat chybu", Width = 140, Height = 30 };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(copyText);
                copy.Text = "Zkopírováno";
            }
            catch (Exception clipboardError)
            {
                MessageBox.Show("Text se nepodařilo zkopírovat: " + clipboardError.Message,
                    "Schránka", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);
        layout.Controls.Add(details, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = close;
        dialog.CancelButton = close;
        dialog.ShowDialog(this);
    }

}
