namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private static readonly string[] QueueFlowStages =
        ["FRONTA", "LIMIT", "SOCKS5 / TOR", "SMTP", "VÝSLEDEK"];

    private void BuildQueueFlow()
    {
        _queueFlowLabels.Clear();
        pnlQueueFlow.Controls.Clear();
        for (var i = 0; i < QueueFlowStages.Length; i++)
        {
            var label = new Label
            {
                Text = QueueFlowStages[i],
                AutoSize = false,
                Width = i == 2 ? 112 : 82,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Gainsboro,
                ForeColor = Color.DimGray,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Margin = new Padding(2, 3, 2, 3)
            };
            _queueFlowLabels[QueueFlowStages[i]] = label;
            pnlQueueFlow.Controls.Add(label);
            if (i < QueueFlowStages.Length - 1)
                pnlQueueFlow.Controls.Add(new Label
                {
                    Text = "›", AutoSize = true, ForeColor = Color.Gray,
                    Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                    Margin = new Padding(0, 3, 0, 0)
                });
        }
    }

    private void UpdateQueueFlow(MessageStep? step)
    {
        if (_queueFlowLabels.Count == 0)
            return;

        var active = step switch
        {
            MessageStep.Queued => 0,
            MessageStep.WaitingRateLimit => 1,
            MessageStep.RentingConnection => 2,
            MessageStep.BuildingMime => 2,
            MessageStep.SmtpSend => 3,
            MessageStep.Succeeded or MessageStep.FailedFinal or MessageStep.Cancelled => 4,
            MessageStep.FailedTransient => 3,
            _ => -1
        };

        foreach (var (name, label) in _queueFlowLabels)
        {
            var index = Array.IndexOf(QueueFlowStages, name);
            label.BackColor = index < active ? Color.FromArgb(218, 238, 225)
                : index == active ? Color.FromArgb(255, 224, 153)
                : Color.Gainsboro;
            label.ForeColor = Color.FromArgb(55, 65, 75);
        }

        if (step == MessageStep.Succeeded)
            _queueFlowLabels["VÝSLEDEK"].BackColor = Color.FromArgb(166, 220, 180);
        else if (step is MessageStep.FailedFinal or MessageStep.FailedTransient)
            _queueFlowLabels["VÝSLEDEK"].BackColor = Color.FromArgb(245, 180, 180);
        else if (step == MessageStep.Cancelled)
            _queueFlowLabels["VÝSLEDEK"].BackColor = Color.LightGray;
    }

    private System.Windows.Forms.Timer? _torStatusTimer;
    private bool _torVerified;
    private bool _torCheckRunning;
    private string _torEndpointKey = string.Empty;
    private string? _torExitIp;
    private string _torDetail = "Tor zatím nebyl ověřen.";

    private static bool IsLocalTorEndpoint(string host, int port) =>
        (host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
         || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
         || host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        && port is 9050 or 9150;

    private static bool IsTorMode(MailTestOptions options) =>
        string.IsNullOrWhiteSpace(options.ProxyList)
        && options.UseSocks5Proxy
        && IsLocalTorEndpoint(options.ProxyHost, options.ProxyPort);

    private bool TryGetConfiguredTorEndpoint(out ProxyEndpoint? endpoint)
    {
        endpoint = null;
        if (!string.IsNullOrWhiteSpace(txtProxyList?.Text) || chkSocks5?.Checked != true)
            return false;

        var host = txtProxyHost.Text.Trim();
        var port = (int)numProxyPort.Value;
        if (!IsLocalTorEndpoint(host, port))
            return false;

        endpoint = new ProxyEndpoint(ProxyType.Socks5, host, port,
            string.IsNullOrWhiteSpace(txtProxyUser.Text) ? null : txtProxyUser.Text,
            string.IsNullOrWhiteSpace(txtProxyPass.Text) ? null : txtProxyPass.Text);
        return true;
    }

    private void UpdateProxyStatus(bool smtpProxyWasUsed = false)
    {
        if (lblProxyStatus is null || lblProxyStatus.IsDisposed)
            return;

        var listConfigured = !string.IsNullOrWhiteSpace(txtProxyList?.Text);
        var singleConfigured = chkSocks5?.Checked == true;
        var enabled = listConfigured || singleConfigured;

        if (!enabled)
        {
            _torVerified = false;
            _torExitIp = null;
            _torEndpointKey = string.Empty;
            _torStatusTimer?.Stop();
            lblProxyStatus.Text = "🧅 TOR VYPNUTO — přímé připojení; výstupní IP není chráněna Tor.";
            lblProxyStatus.BackColor = Color.FromArgb(232, 232, 232);
            lblProxyStatus.ForeColor = Color.DimGray;
            return;
        }

        if (TryGetConfiguredTorEndpoint(out var endpoint) && endpoint is not null)
        {
            var key = endpoint.DisplayKey;
            if (!key.Equals(_torEndpointKey, StringComparison.Ordinal))
            {
                _torEndpointKey = key;
                _lastTorCheckUtc = DateTime.MinValue;
                _torVerified = false;
                _torExitIp = null;
                _torDetail = "Ověřuji Tor exit přes SOCKS5.";
            }

            EnsureTorStatusTimer();
            RenderTorStatus();
            if (!_torCheckRunning && (DateTime.UtcNow - _lastTorCheckUtc).TotalSeconds >= 15)
                _ = VerifyConfiguredTorAsync();
            return;
        }

        _torStatusTimer?.Stop();
        _torVerified = false;
        _torExitIp = null;
        var host = listConfigured ? "seznam proxy" : txtProxyHost.Text.Trim();
        var port = listConfigured ? 0 : (int)numProxyPort.Value;
        lblProxyStatus.Text = $"PROXY KONFIGUROVÁNA ({host}{(port > 0 ? ":" + port : "")}) — Tor exit není ověřen.";
        lblProxyStatus.BackColor = Color.FromArgb(255, 239, 194);
        lblProxyStatus.ForeColor = Color.FromArgb(110, 75, 0);
    }

    private DateTime _lastTorCheckUtc = DateTime.MinValue;

    private void EnsureTorStatusTimer()
    {
        if (_torStatusTimer is null)
        {
            _torStatusTimer = new System.Windows.Forms.Timer { Interval = 20_000 };
            _torStatusTimer.Tick += async (_, _) => await VerifyConfiguredTorAsync();
        }
        if (!_torStatusTimer.Enabled)
            _torStatusTimer.Start();
    }

    private async Task VerifyConfiguredTorAsync()
    {
        if (_torCheckRunning || !TryGetConfiguredTorEndpoint(out var endpoint) || endpoint is null)
            return;

        var key = endpoint.DisplayKey;
        _torCheckRunning = true;
        _lastTorCheckUtc = DateTime.UtcNow;
        RenderTorStatus();
        try
        {
            var result = await TorProxyVerifier.VerifyAsync(endpoint);
            if (!IsDisposed && key.Equals(_torEndpointKey, StringComparison.Ordinal))
            {
                _torVerified = result.IsTor;
                _torExitIp = result.ExitIp;
                _torDetail = result.Detail;
                RenderTorStatus();
            }
        }
        finally
        {
            _torCheckRunning = false;
        }
    }

    private void RenderTorStatus()
    {
        if (lblProxyStatus is null || lblProxyStatus.IsDisposed)
            return;

        if (_torVerified)
        {
            lblProxyStatus.Text = $"🧅 TOR AKTIVNÍ — exit IP: {_torExitIp ?? "ověřena"} · kontrola přes Tor Project.";
            lblProxyStatus.BackColor = Color.FromArgb(205, 240, 210);
            lblProxyStatus.ForeColor = Color.DarkGreen;
        }
        else if (_torCheckRunning)
        {
            lblProxyStatus.Text = "🧅 TOR OVĚŘUJI — SMTP test zatím nespouštěj.";
            lblProxyStatus.BackColor = Color.FromArgb(255, 239, 194);
            lblProxyStatus.ForeColor = Color.FromArgb(110, 75, 0);
        }
        else
        {
            lblProxyStatus.Text = "🧅 TOR NENÍ OVĚŘEN — " + _torDetail + " Odesílání přes Tor bude zablokováno, dokud ověření neprojde.";
            lblProxyStatus.BackColor = Color.FromArgb(250, 220, 220);
            lblProxyStatus.ForeColor = Color.DarkRed;
        }
    }

    private async Task<bool> EnsureTorVerifiedBeforeStartAsync(MailTestOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ProxyList) || !options.UseSocks5Proxy
            || !IsLocalTorEndpoint(options.ProxyHost, options.ProxyPort))
            return true;

        var endpoint = new ProxyEndpoint(ProxyType.Socks5, options.ProxyHost, options.ProxyPort,
            string.IsNullOrWhiteSpace(options.ProxyUsername) ? null : options.ProxyUsername,
            string.IsNullOrWhiteSpace(options.ProxyPassword) ? null : options.ProxyPassword);
        var result = await TorProxyVerifier.VerifyAsync(endpoint);
        _torEndpointKey = endpoint.DisplayKey;
        _torVerified = result.IsTor;
        _torExitIp = result.ExitIp;
        _torDetail = result.Detail;
        RenderTorStatus();
        if (!result.IsTor)
        {
            ShowErrorWithCopy(
                "SMTP test nebyl spuštěn, protože Tor nebyl ověřen. Aplikace nesmí při požadavku na Tor potichu přejít na přímé připojení.\r\n\r\n"
                + result.Detail,
                "Tor není ověřen",
                $"Proxy: {endpoint.DisplayKey}\r\nVýstupní IP: {result.ExitIp ?? "nezjištěna"}",
                GuiErrorCodes.Validation);
            return false;
        }
        return true;
    }
}
