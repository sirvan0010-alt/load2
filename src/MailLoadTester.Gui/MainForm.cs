namespace MailLoadTester.Gui;

public sealed partial class MainForm : Form
{
    // === Controls ===
    private Button btnStart = null!, btnStop = null!, btnSaveProfile = null!, btnLoadProfile = null!;
    private TabControl tabs = null!;
    private ToolTip toolTip = null!;

    // UI enhancement
    private Panel pnlSafetyBanner = null!;
    private Label lblSafetyBanner = null!;
    private Button btnWizard = null!;
    private CheckBox chkDarkMode = null!;
    private bool _darkMode;

    // SMTP tab
    private TextBox txtHost = null!;
    private NumericUpDown numPort = null!;
    private ComboBox cmbSecurity = null!;
    private CheckBox chkIgnoreCert = null!;
    private CheckBox chkAuth = null!;
    private ComboBox cmbAuthMethod = null!;
    private TextBox txtUser = null!, txtPass = null!;
    private Button btnTestConnection = null!, btnSendTest = null!;
    private TextBox txtSourceIp = null!;
    private ComboBox cmbLocalIp = null!;
    private TextBox txtIpv6Prefix = null!;
    private NumericUpDown numIpv6PrefixLen = null!;
    private TextBox txtIpv4Rotation = null!;
    private CheckBox chkIpv4RotationRandom = null!;
    private TextBox txtClientCert = null!, txtClientCertPass = null!;
    private NumericUpDown numConnectTimeout = null!, numReadTimeout = null!;

    // Message tab
    private TextBox txtFrom = null!, txtTo = null!, txtCc = null!, txtBcc = null!;
    private TextBox txtDisplayName = null!, txtSubject = null!, txtBody = null!;
    private CheckBox chkHtmlBody = null!, chkRandomData = null!, chkSmtpUtf8 = null!;
    private CheckBox chkBogusData = null!, chkRandomHtml = null!, chkRandomAttachments = null!, chkVaryMessage = null!;
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
        Text = $"MailLoadTester {MailLoadTester.AppVersion.Current} — Maximum Brutal Edition";
        Size = new Size(1280, 920);
        MinimumSize = new Size(1000, 750);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular);
        InitializeComponent();
        WireEvents();
        SetDefaults();
        UpdateSafetyBanner();
        ApplyTheme(false);
        FormClosing += MainForm_FormClosing;
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
        if (!runActive && !connectionTestActive && !mailboxMonitorActive)
            return;

        e.Cancel = true;
        lblStatus.Text = "Zavírám — čekám na ukončení běžící operace…";
        _cts?.Cancel();
        _testConnectionCts?.Cancel();
        _mailboxMonitorTimer?.Stop();
        _mailboxMonitorCts?.Cancel();

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

        _closeApproved = true;
        if (!IsDisposed)
            Close();
    }
}
