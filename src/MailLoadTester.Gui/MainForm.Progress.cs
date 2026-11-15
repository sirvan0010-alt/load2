namespace MailLoadTester.Gui;

public sealed partial class MainForm
{

    private void UpdateProgress(ProgressUpdate u)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { try { Invoke(UpdateProgress, u); } catch (ObjectDisposedException) { } catch (InvalidOperationException) { } return; }
        if (IsDisposed) return;

        var done = u.Sent + u.Failed;
        var total = Math.Max(1, (int)numCount.Value);
        progressBar.Value = Math.Min(100, (int)(done * 100.0 / total));
        lblSentFailed.Text = $"OK:{u.Sent} FAIL:{u.Failed}";
        lblStatus.Text = u.CurrentStep;
        lblCapacityRun.Text = u.CapacityMimeBytesReserved.HasValue
            ? $"MIME: {FormatMailboxBytes(u.CapacityMimeBytesReserved)} / {(u.CapacityMimeBytesTarget.HasValue ? FormatMailboxBytes(u.CapacityMimeBytesTarget) : "bez limitu")}"
            : string.Empty;
        if (u.EtaSeconds.HasValue)
            lblEta.Text = $"ETA: {u.EtaSeconds.Value:F0}s";

        if (u.WorkerId.HasValue && _workerLabels != null)
        {
            int idx = u.WorkerId.Value - 1;
            if (idx >= 0 && idx < _workerLabels.Length)
            {
                var lbl = _workerLabels[idx];
                lbl.Text = $"W{u.WorkerId.Value}: {u.CurrentStep}";
                lbl.BackColor = u.MessageStep switch
                {
                    MessageStep.Succeeded => Color.FromArgb(200, 255, 200),
                    MessageStep.FailedFinal => Color.FromArgb(255, 200, 200),
                    MessageStep.FailedTransient => Color.FromArgb(255, 235, 180),
                    _ => Color.White
                };
            }
        }

        if (u.MessageStep.HasValue)
        {
            int stepIdx = u.MessageStep.Value switch
            {
                MessageStep.Queued => 0,
                MessageStep.WaitingRateLimit => 1,
                MessageStep.RentingConnection => 2,
                MessageStep.BuildingMime => 3,
                MessageStep.SmtpSend => 4,
                MessageStep.Cancelled => 5,
                MessageStep.Succeeded => 5,
                MessageStep.FailedTransient => 5,
                MessageStep.FailedFinal => 5,
                _ => -1
            };
            if (stepIdx >= 0)
            {
                for (int i = 0; i < _checklistLabels.Length; i++)
                    _checklistLabels[i].BackColor = i <= stepIdx ? Color.LightGreen : Color.LightGray;
            }
        }

        if (u.PathStep.HasValue && _pathLabels.TryGetValue(u.PathStep.Value, out var pathLabel))
        {
            foreach (var pair in _pathLabels)
                pair.Value.BackColor = Color.Gainsboro;
            pathLabel.BackColor = u.PathStepSuccess switch
            {
                true => Color.LightGreen,
                false => Color.LightCoral,
                _ => Color.Khaki
            };
        }

        if (u.ObservedSnapshot is not null && gridObserved != null)
        {
            try
            {
                gridObserved.Rows.Clear();
                foreach (var observed in u.ObservedSnapshot.Take(200))
                {
                    gridObserved.Rows.Add(
                        observed.SmtpCode,
                        observed.SampleText,
                        observed.Count,
                        observed.Classification.ToString(),
                        observed.RecommendedAction);
                }
            }
            catch { /* GUI-only telemetry must never fail the run */ }
        }
    }

    private void ResetDeliveryPath()
    {
        foreach (var label in _pathLabels.Values)
            label.BackColor = Color.Gainsboro;
        foreach (var label in _checklistLabels)
            label.BackColor = Color.LightGray;
    }

    private void BuildDeliveryPathPipeline()
    {
        _pathLabels.Clear();
        foreach (var step in _pathOrder)
        {
            var label = new Label
            {
                Text = step switch
                {
                    DeliveryStepKind.DnsMxLookup => "DNS/MX",
                    DeliveryStepKind.TcpConnect => "TCP",
                    DeliveryStepKind.Ehlo => "EHLO",
                    DeliveryStepKind.StartTls => "STARTTLS",
                    DeliveryStepKind.Auth => "AUTH",
                    DeliveryStepKind.MailFrom => "MAIL FROM",
                    DeliveryStepKind.RcptTo => "RCPT TO",
                    DeliveryStepKind.Data => "DATA",
                    DeliveryStepKind.Quit => "QUIT",
                    _ => step.ToString()
                },
                AutoSize = true,
                Padding = new Padding(5, 3, 5, 3),
                Margin = new Padding(2),
                BackColor = Color.Gainsboro,
                ForeColor = Color.DimGray,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 8.5f)
            };
            _pathLabels[step] = label;
            pnlDeliveryPath.Controls.Add(label);
            if (step != _pathOrder[^1])
                pnlDeliveryPath.Controls.Add(new Label { Text = "→", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(0, 3, 0, 3) });
        }
    }

    private void ApplyProviderPreset()
    {
        var selected = cmbProviderPreset?.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(selected)) return;
        var preset = ProviderPresets.All.FirstOrDefault(p => p.DisplayName == selected);
        if (preset is null) return;
        chkJitter.Checked = preset.EnableJitter;
        numJitterPercent.Value = Math.Clamp(preset.JitterPercent, numJitterPercent.Minimum, numJitterPercent.Maximum);
        chkBurst.Checked = preset.EnableBurstMode;
        numBurstSize.Value = Math.Clamp(preset.BurstSize, numBurstSize.Minimum, numBurstSize.Maximum);
        numBurstPause.Value = Math.Clamp(preset.BurstPauseSeconds, numBurstPause.Minimum, numBurstPause.Maximum);
        chkBackoff.Checked = preset.EnableProgressiveBackoff;
        numBackoffAfter.Value = Math.Clamp(preset.BackoffAfterSuccesses, numBackoffAfter.Minimum, numBackoffAfter.Maximum);
        numBackoffMult.Value = Math.Clamp((decimal)(preset.BackoffMultiplier * 10), numBackoffMult.Minimum, numBackoffMult.Maximum);
        numMaxInterval.Value = Math.Clamp(preset.MaxIntervalMs, numMaxInterval.Minimum, numMaxInterval.Maximum);

        chkWarmup.Checked = preset.EnableWarmup;
        txtWarmupPhases.Text = preset.WarmupPhases;
        chkGreylist.Checked = preset.DetectGreylist;
        numGreylistMinutes.Value = Math.Clamp(preset.GreylistRetryMinutes, numGreylistMinutes.Minimum, numGreylistMinutes.Maximum);
                lblPaceStatus.Text = $"Stav tempa: {preset.DisplayName}";
    }

    private async Task OnCheckRblAsync()
    {
        var ip = txtSourceIp.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            MessageBox.Show("Nejdříve vyplňte Source IP na záložce SMTP Server.", "RBL", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        btnCheckRbl.Enabled = false;
        lblRblResult.Text = "Kontroluji Spamhaus Zen…";
        lblRblResult.ForeColor = Color.DimGray;
        try
        {
            var result = await RblChecker.CheckSpamhausZenAsync(ip);
            lblRblResult.Text = result.IsListed ? "⚠ " + result.Detail
                : result.IsUnknown ? "? " + result.Detail
                : "✓ " + result.Detail;
            lblRblResult.ForeColor = result.IsListed ? Color.DarkRed
                : result.IsUnknown ? Color.DarkOrange
                : Color.DarkGreen;
        }
        catch (Exception ex)
        {
            lblRblResult.Text = "Chyba RBL: " + ex.Message;
            lblRblResult.ForeColor = Color.DarkRed;
        }
        finally
        {
            btnCheckRbl.Enabled = true;
        }
    }

    private void AppendLog(string message, Color color)
    {
        if (IsDisposed || rtbLog is null || rtbLog.IsDisposed)
            return;

        if (rtbLog.InvokeRequired)
        {
            try
            {
                rtbLog.BeginInvoke(new Action(() => AppendLog(message, color)));
            }
            catch (InvalidOperationException)
            {
                // The form may be closing while a background operation completes.
            }
            return;
        }

        rtbLog.SelectionStart = rtbLog.TextLength;
        rtbLog.SelectionLength = 0;
        rtbLog.SelectionColor = color;
        rtbLog.AppendText(message + Environment.NewLine);
        rtbLog.SelectionColor = rtbLog.ForeColor;
        rtbLog.ScrollToCaret();
    }

}
