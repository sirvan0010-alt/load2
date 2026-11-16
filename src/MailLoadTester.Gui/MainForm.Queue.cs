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

    private void SetTorIndicator(TorIndicatorState state, string? exitIp = null)
    {
        if (torStatusIndicator is null || torStatusIndicator.IsDisposed)
            return;

        torStatusIndicator.ExitIp = exitIp;
        torStatusIndicator.State = state;
        if (torStatusHost is not null)
        {
            torStatusHost.ToolTipText = state switch
            {
                TorIndicatorState.Verified => $"Tor ověřen přes check.torproject.org. Výstupní IP: {exitIp}.",
                TorIndicatorState.Checking => "Ověřuji lokální SOCKS5 a Tor výstupní IP…",
                TorIndicatorState.Failed => "Tor ověření selhalo. Přímé připojení není povoleno.",
                _ => "Tor není aktivní ani ověřen."
            };
        }
    }

    private void InvalidateTorVerification()
    {
        if (_torVerified)
        {
            _torVerified = false;
            _torExitIp = null;
            SetTorIndicator(chkRequireTor?.Checked == true
                ? TorIndicatorState.Checking
                : TorIndicatorState.Off);
        }
    }

    private void UpdateProxyStatus(bool smtpProxyWasUsed = false)
    {
        if (lblProxyStatus is null || lblProxyStatus.IsDisposed)
            return;

        var listConfigured = !string.IsNullOrWhiteSpace(txtProxyList?.Text);
        var singleConfigured = chkSocks5?.Checked == true;
        var enabled = listConfigured || singleConfigured;
        if (chkRequireTor?.Checked == true)
        {
            if (_torVerified && !string.IsNullOrWhiteSpace(_torExitIp))
            {
                lblProxyStatus.Text = $"● TOR OVĚŘEN — 127.0.0.1:{numProxyPort.Value}, výstupní IP: {_torExitIp}. Při výpadku bez přímého fallbacku.";
                lblProxyStatus.BackColor = Color.FromArgb(225, 215, 250);
                lblProxyStatus.ForeColor = Color.FromArgb(82, 35, 145);
                SetTorIndicator(TorIndicatorState.Verified, _torExitIp);
            }
            else
            {
                lblProxyStatus.Text = "● TOR POVINNÝ, ALE NEOVĚŘENÝ — před spuštěním se ověří Tor exit IP; při chybě se SMTP nespustí.";
                lblProxyStatus.BackColor = Color.FromArgb(255, 239, 194);
                lblProxyStatus.ForeColor = Color.FromArgb(110, 75, 0);
                if (torStatusIndicator?.State != TorIndicatorState.Checking)
                    SetTorIndicator(TorIndicatorState.Off);
            }
            return;
        }

        if (!enabled)
        {
            lblProxyStatus.Text = "● PROXY VYPNUTA — SMTP spojení půjde běžnou sítí.";
            lblProxyStatus.BackColor = Color.FromArgb(250, 220, 220);
            lblProxyStatus.ForeColor = Color.DarkRed;
            SetTorIndicator(TorIndicatorState.Off);
            return;
        }

        var host = listConfigured ? "seznam proxy" : txtProxyHost.Text.Trim();
        var port = listConfigured ? 0 : (int)numProxyPort.Value;
        var isLocalTorEndpoint = !listConfigured
            && (host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            && port is 9050 or 9150;
        var kind = isLocalTorEndpoint ? "Tor SOCKS5" : "SOCKS5 proxy";
        lblProxyStatus.Text = smtpProxyWasUsed
            ? $"● {kind} použita pro SMTP spojení — výstupní IP zatím NEOVĚŘENA."
            : $"● {kind} nakonfigurována — spojení a výstupní IP zatím NEOVĚŘENY.";
        lblProxyStatus.BackColor = Color.FromArgb(255, 239, 194);
        lblProxyStatus.ForeColor = Color.FromArgb(110, 75, 0);
        SetTorIndicator(TorIndicatorState.Off);
    }
}
