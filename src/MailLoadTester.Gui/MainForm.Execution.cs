namespace MailLoadTester.Gui;

public sealed partial class MainForm
{

    private static void EnsureRealSmtpConfiguration(MailTestOptions options)
    {
        static bool IsExampleDomain(string value)
        {
            var at = value.LastIndexOf('@');
            var domain = at >= 0 ? value[(at + 1)..] : value;
            return domain.Equals("example.com", StringComparison.OrdinalIgnoreCase)
                || domain.Equals("example.net", StringComparison.OrdinalIgnoreCase)
                || domain.Equals("example.org", StringComparison.OrdinalIgnoreCase)
                || domain.EndsWith(".example", StringComparison.OrdinalIgnoreCase)
                || domain.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase);
        }

        if (!options.DirectMxDelivery &&
            (options.SmtpHost.Equals("smtp.example.com", StringComparison.OrdinalIgnoreCase)
             || options.SmtpHost.EndsWith(".example", StringComparison.OrdinalIgnoreCase)
             || options.SmtpHost.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                $"Aplikace stále používá ukázkový SMTP server ('{options.SmtpHost}'). Zadej skutečný server poskytovatele do pole SMTP Host na kartě SMTP, například smtp.seznam.cz nebo smtp.gmail.com. Pokud už jsi ho změnil, zkontroluj načtený profil a nastavení účtů.");

        if (IsExampleDomain(options.From) || options.Recipients.Any(IsExampleDomain))
            throw new InvalidOperationException(
                "Odesílatel nebo příjemce stále používá ukázkovou doménu example.com / example.net / example.org. Zadej skutečné e-mailové adresy.");
    }

    private MailTestOptions BuildOptions()
    {
        var recipients = txtTo.Text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cc = string.IsNullOrWhiteSpace(txtCc.Text) ? Array.Empty<string>() : txtCc.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bcc = string.IsNullOrWhiteSpace(txtBcc.Text) ? Array.Empty<string>() : txtBcc.Text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var attachments = lstAttachments.Items.Cast<string>().ToList();
        var inline = lstInlineAttachments.Items.Cast<string>().ToList();
        var headers = Validation.ParseHeaders(txtHeaders.Text);
        var security = Enum.Parse<SmtpSecurity>(cmbSecurity.SelectedItem?.ToString() ?? "StartTls", true);
        var authMethod = Enum.Parse<SmtpAuthMethod>(cmbAuthMethod.SelectedItem?.ToString() ?? "Auto", true);
        var ipVersion = Enum.Parse<IpVersionPreference>(cmbIpVersion.SelectedItem?.ToString() ?? "Any", true);

        return new MailTestOptions(
            txtFrom.Text.Trim(), recipients.ToList(),
            txtHost.Text.Trim(), (int)numPort.Value, security,
            chkAuth.Checked, txtUser.Text, txtPass.Text,
            (int)numCount.Value, (int)numInterval.Value,
            chkBatchMode.Checked, (int)numBatchSize.Value, (int)numBatchPause.Value,
            (int)numConcurrency.Value, txtSubject.Text, txtBody.Text, txtDisplayName.Text,
            chkRandomData.Checked, chkTestMode.Checked, txtAllowedDomains.Text,
            chkHtmlBody.Checked, attachments, headers, chkIgnoreCert.Checked, (int)numRetries.Value, chkDryRun.Checked,
            chkSocks5.Checked, txtProxyHost.Text, (int)numProxyPort.Value, txtProxyUser.Text, txtProxyPass.Text, ProxyList: txtProxyList?.Text ?? "", ProxyListRandom: chkProxyListRandom?.Checked ?? false, ProxyBanMinutes: (int)(numProxyBanMinutes?.Value ?? 15),
            chkDirectMx.Checked, chkPreWarm.Checked, chkAdaptive.Checked, chkCircuit.Checked, (int)numCircuitThreshold.Value,
            CircuitBreakerWindowSize: _circuitWindowSize,
            CircuitBreakerFailurePercent: _circuitFailurePercent,
            EnableDashboard: chkDashboard.Checked, DashboardPort: (int)numDashboardPort.Value,
            AuthMethod: authMethod, SourceIp: txtSourceIp.Text.Trim(), Ipv6Prefix: txtIpv6Prefix?.Text.Trim() ?? "", Ipv6PrefixLength: (int)(numIpv6PrefixLen?.Value ?? 64), Ipv4Rotation: txtIpv4Rotation?.Text.Trim() ?? "", Ipv4RotationRandom: chkIpv4RotationRandom?.Checked ?? false, ClientCertificatePath: txtClientCert.Text,
            ClientCertificatePassword: txtClientCertPass.Text,
            CcRecipients: cc.ToList(), BccRecipients: bcc.ToList(), SmtpUtf8: chkSmtpUtf8.Checked,
            InlineAttachments: inline,
            WebhookUrl: txtWebhookUrl.Text, BandwidthLimitKbps: (int)numBandwidthLimit.Value, IpVersion: ipVersion,
            ConnectTimeoutMs: (int)numConnectTimeout.Value, ReadTimeoutMs: (int)numReadTimeout.Value,
            EnableSessionLog: chkSessionLog.Checked, SessionLogPath: txtSessionLogPath.Text,
            AutoRestartOnFailure: chkAutoRestart.Checked, AutoRestartMaxAttempts: (int)numAutoRestartAttempts.Value,
            EmlTemplatePath: string.IsNullOrWhiteSpace(txtEmlTemplate.Text) ? null : txtEmlTemplate.Text,
            IdleConnectionHealthCheckSeconds: _idleHealthCheckSeconds,
            UseBogusData: chkBogusData.Checked, GenerateRandomHtml: chkRandomHtml.Checked,
            GenerateRandomAttachments: chkRandomAttachments.Checked,
            MaxRandomAttachments: (int)numMaxRandomAttachments.Value, VarySubjectBodyPerMessage: chkVaryMessage.Checked,
            RandomAttachmentSizeMb: (int)numRandomAttachmentSizeMb.Value,
            PaceProfile: ProviderPresets.All.FirstOrDefault(p => p.DisplayName == (cmbProviderPreset.SelectedItem?.ToString() ?? ""))?.Id
                ?? cmbProviderPreset.SelectedItem?.ToString() ?? "Standard",
            EnableJitter: chkJitter.Checked,
            JitterPercent: (int)numJitterPercent.Value,
            EnableBurstMode: chkBurst.Checked,
            BurstSize: (int)numBurstSize.Value,
            BurstPauseSeconds: (int)numBurstPause.Value,
            EnableProgressiveBackoff: chkBackoff.Checked,
            BackoffAfterSuccesses: (int)numBackoffAfter.Value,
            BackoffMultiplier: (int)numBackoffMult.Value / 10.0,
            MaxIntervalMs: (int)numMaxInterval.Value,
            EnablePerRecipientLimit: chkPerRecipient.Checked,
            MaxMessagesPerRecipient: (int)numMaxPerRecipient.Value,
            PerRecipientWindowMinutes: (int)numPerRecipientWindow.Value,
            EnableSendingTimeWindow: chkTimeWindow.Checked,
            SendingWindowFromHour: (int)numWindowFrom.Value,
            SendingWindowToHour: (int)numWindowTo.Value,
            EnableWarmup: chkWarmup.Checked,
            WarmupPhases: txtWarmupPhases.Text,
            DetectGreylist: chkGreylist.Checked,
            GreylistRetryMinutes: (int)numGreylistMinutes.Value,
            MaxGreylistRetries: (int)numGreylistRetries.Value,
            ProviderPreset: ProviderPresets.All.FirstOrDefault(p => p.DisplayName == (cmbProviderPreset.SelectedItem?.ToString() ?? ""))?.Id
                ?? cmbProviderPreset.SelectedItem?.ToString() ?? "Custom",
            CheckRblBeforeStart: chkRbl.Checked,
            CollectObservedResponses: chkCollectObserved.Checked,
            // SEC-003/004: GUI Start without Test mode is an explicit operator acknowledgement.
            // CLI --unauthorized also grants live send outside Test mode.
            Unauthorized: !chkTestMode.Checked
                || AuthorizationGate.HasUnauthorizedFlag(Environment.GetCommandLineArgs()),
            Accounts: BuildAccountsFromUi(),
            CapacityRun: new CapacityRunLimits(
                Enabled: chkCapacityRun.Checked,
                MaxMessageCount: (int)numCapacityMaxMessages.Value,
                TargetMimeBytes: decimal.ToInt64(numCapacityTargetMiB.Value * 1024m * 1024m),
                MaxMimeMessageBytes: decimal.ToInt64(numCapacityMaxMessageMiB.Value * 1024m * 1024m),
                MaxMessagesPerMinute: (int)numCapacityMaxPerMinute.Value,
                ValidateBeforeRun: chkCapacityStartupValidation.Checked),
            RequireTor: chkRequireTor.Checked);
    }

    private void UpdateCapacityEstimate(MailTestOptions options)
    {
        if (options.CapacityRun is not { Enabled: true } limits)
        {
            lblCapacityEstimate.Text = "Capacity Run je vypnutý.";
            return;
        }

        try
        {
            var paths = (options.Attachments ?? Array.Empty<string>())
                .Concat(options.InlineAttachments ?? Array.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var attachmentSizes = new List<long>(paths.Length);
            foreach (var path in paths)
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    throw new FileNotFoundException("Příloha pro odhad neexistuje.", path);
                attachmentSizes.Add(info.Length);
            }

            var randomCount = 0;
            long randomAttachmentBytes = 0;
            if (options.GenerateRandomAttachments)
            {
                var safety = AttachmentPlanner.EstimateRandomAttachments(
                    options.RandomAttachmentSizeMb,
                    options.MaxRandomAttachments,
                    options.MaxConcurrency);
                if (!safety.IsSafe)
                {
                    lblCapacityEstimate.Text = "Odhad není úplný: plán náhodných příloh je blokován bezpečnostním limitem RAM. " + safety.Explanation;
                    return;
                }

                var randomSizeMb = AttachmentPlanner.ResolveRandomAttachmentSizeMb(
                    options.RandomAttachmentSizeMb,
                    options.MaxRandomAttachments,
                    options.MaxConcurrency);
                randomCount = Math.Clamp(options.MaxRandomAttachments, 1, 5);
                randomAttachmentBytes = checked((long)randomSizeMb * 1024L * 1024L);
            }

            var hasUnaccountedContent = !string.IsNullOrWhiteSpace(options.EmlTemplatePath)
                || options.RandomTestData
                || options.GenerateRandomHtml;
            var estimate = CapacityRunPreview.Estimate(
                attachmentSizes,
                options.MessageCount,
                limits.MaxMessageCount,
                randomCount,
                randomAttachmentBytes,
                hasUnaccountedContent);

            var warnings = new List<string>();
            if (limits.MaxMimeMessageBytes > 0 && estimate.EstimatedMimeBytesPerMessage > limits.MaxMimeMessageBytes)
                warnings.Add("odhad jedné zprávy překračuje nastavený limit");
            if (limits.TargetMimeBytes > 0 && estimate.EstimatedMimeBytesForLogicalMessages > limits.TargetMimeBytes)
                warnings.Add("odhad celkového objemu překračuje nastavený rozpočet ještě před retry");

            var warningText = warnings.Count == 0 ? string.Empty : " VAROVÁNÍ: " + string.Join("; ", warnings) + ".";
            lblCapacityEstimate.Text =
                $"Odhad MIME: {FormatMailboxBytes(estimate.EstimatedMimeBytesPerMessage)} / zpráva; " +
                $"{FormatMailboxBytes(estimate.EstimatedMimeBytesForLogicalMessages)} pro {estimate.LogicalMessageCount:N0} zpráv bez retry. " +
                estimate.Note + warningText;
        }
        catch (Exception ex)
        {
            lblCapacityEstimate.Text = "Odhad MIME není k dispozici: " + ex.Message;
        }
    }

    private async Task OnStartAsync()
    {
        if (_isRunning) return;
        MailTestOptions options;
        try
        {
            options = BuildOptions();
            Validation.Validate(options);
            EnsureRealSmtpConfiguration(options);
            _activeRunMessageCount = options.MessageCount;
            UpdateCapacityEstimate(options);
        }
        catch (Exception ex)
        {
            ShowErrorWithCopy("Chyba validace:\n" + ex.Message, "Nelze spustit", ex.ToString(), GuiErrorCodes.Validation);
            return;
        }

        // Local DNS preflight is deliberately skipped in strict Tor mode.
        if (!options.RequireTor)
        {
        // Pre-flight: SPF + DMARC pro doménu odesílatele (DnsClient.NET)
        try
        {
            var domain = DnsPolicyChecker.ExtractDomain(txtFrom.Text.Trim());
            if (!string.IsNullOrEmpty(domain))
            {
                lblStatus.Text = $"Kontroluji SPF/DMARC pro {domain}…";
                var policy = await DnsPolicyChecker.CheckAsync(domain);
                if (!policy.HasSpf)
                {
                    var text = policy.SpfCheckFailed
                        ? $"Nepodařilo se ověřit SPF záznam domény {domain} (DNS dotaz selhal).\n" +
                          "Nejde o potvrzeně chybějící SPF — DNS prostě teď neodpověděl.\n\n" +
                          "Chcete i přesto spustit test?"
                        : $"Doména {domain} nemá platný SPF záznam (TXT v=spf1).\n" +
                          "Bez SPF Gmail/Microsoft často zprávy zahodí nebo označí jako spam a poškodí reputaci IP/domény.\n\n" +
                          "Chcete i přesto spustit test?";
                    var r = MessageBox.Show(text,
                        policy.SpfCheckFailed ? "SPF neověřeno" : "Varování: Chybí SPF",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
                }
                if (!policy.HasDmarc)
                {
                    var text = policy.DmarcCheckFailed
                        ? $"Nepodařilo se ověřit DMARC politiku domény {domain} (DNS dotaz selhal).\n" +
                          "Nejde o potvrzeně chybějící DMARC — DNS prostě teď neodpověděl.\n\n" +
                          "Chcete i přesto spustit test?"
                        : $"Doména {domain} nemá DMARC politiku (_dmarc.{domain}, TXT v=DMARC1).\n" +
                          "Pro hromadné testy vůči Gmail/Yahoo je DMARC často vyžadován.\n\n" +
                          "Chcete i přesto spustit test?";
                    var r = MessageBox.Show(text,
                        policy.DmarcCheckFailed ? "DMARC neověřeno" : "Varování: Chybí DMARC",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
                }
            }
        }
        catch (Exception ex)
        {
            var r = MessageBox.Show(
                $"Kontrola SPF/DMARC selhala ({ex.Message}).\nPokračovat bez ověření?",
                "DNS kontrola", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
        }

        }
        else
        {
            AppendLog("Tor mode: local SPF/DMARC and RBL DNS checks skipped to avoid DNS outside the SOCKS5 tunnel.", Color.LightGreen);
        }

        // Volitelná RBL kontrola source IP
        if (!options.RequireTor && chkRbl != null && chkRbl.Checked && !string.IsNullOrWhiteSpace(txtSourceIp.Text))
        {
            try
            {
                lblStatus.Text = "Kontroluji RBL (Spamhaus)…";
                var rbl = await RblChecker.CheckSpamhausZenAsync(txtSourceIp.Text.Trim());
                if (rbl.IsListed)
                {
                    var r = MessageBox.Show(
                        "Source IP je na blacklistu Spamhaus Zen.\n" + rbl.Detail + "\n\nPřesto spustit test?",
                        "RBL varování", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    if (r == DialogResult.No) { lblStatus.Text = "Připraveno"; return; }
                }
            }
            catch { /* neblokovat start při chybě DNSBL */ }
        }

        if (options.RequireTor)
        {
            // Stop any previously scheduled IMAP polling and cancel an in-flight refresh
            // before opening the strict Tor SMTP run.
            StopMailboxMonitorPolling();
            if (_mailboxRefreshInProgress)
            {
                _mailboxMonitorCts?.Cancel();
                var pendingMailboxRefresh = _mailboxMonitorCompletion;
                if (pendingMailboxRefresh is not null)
                    await pendingMailboxRefresh.Task;
            }
        }

        // Take a real IMAP baseline before SMTP starts. Missing/unsupported metrics remain null.
        _mailboxRunBaseline = null;
        _mailboxLastSnapshot = null;
        if (!options.RequireTor && IsMailboxMonitorConfigured())
            _mailboxRunBaseline = await RefreshMailboxMonitorAsync(enforceQuotaStop: false);

        _isRunning = true;
        _mailboxQuotaStopTriggered = false;
        _lastRunReport = null;
        btnExportReport.Enabled = false;
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        rtbLog.Clear();
        smtpLogTab?.ClearLog();
        AppendLog("=== START TESTU ===", Color.Cyan);
        AppendLog($"SMTP cíl: {options.SmtpHost}:{options.Port} · {options.Security}", Color.LightGray);
        AppendLog(options.DryRun
            ? "Režim: DRY-RUN — žádné skutečné SMTP spojení ani e-maily se neodesílají."
            : "Režim: REÁLNÉ SMTP — aplikace se připojí k serveru a pokusí se odesílat zprávy.", 
            options.DryRun ? Color.DarkOrange : Color.LightGreen);
        AppendLog($"Plán: {options.MessageCount:N0} zpráv · paralelismus {options.MaxConcurrency} · interval {options.IntervalMs} ms · retry {options.MaxRetries}.", Color.LightGray);
        runMetricsChart?.ClearSamples();
        adaptiveControlChart?.ClearSamples();
        _lastRunSmtpSendAttempts = 0;
        _lastRunSmtp4xx = 0;
        _lastRunSmtp5xx = 0;
        _lastRunAccepted = 0;
        progressBar.Value = 0;
        lblStatus.Text = "Běží…";
        lblEta.Text = "ETA: počítám…";
        lblSentFailed.Text = "OK:0 FAIL:0";
        ResetDeliveryPath();
        if (lblPaceStatus != null) lblPaceStatus.Text = "Stav tempa: Běží";
        if (gridObserved != null) gridObserved.Rows.Clear();

        pnlWorkers.Controls.Clear();
        _workerLabels = new Label[(int)numConcurrency.Value];
        for (int w = 0; w < _workerLabels.Length; w++)
        {
            _workerLabels[w] = new Label
            {
                Text = $"W{w + 1}: —", AutoSize = true,
                Padding = new Padding(4, 2, 4, 2), Margin = new Padding(2),
                BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5f)
            };
            pnlWorkers.Controls.Add(_workerLabels[w]);
        }

        _cts = new CancellationTokenSource();
        if (_mailboxRunBaseline is not null && chkMailboxStopAtQuota.Checked
            && MailboxMonitorMath.ShouldStop(_mailboxRunBaseline.QuotaUsagePercent, (int)numMailboxStopPercent.Value))
        {
            _mailboxQuotaStopTriggered = true;
            AppendLog($"IMAP výchozí stav již dosahuje limitu { (int)numMailboxStopPercent.Value } %. SMTP test nebude spuštěn.", Color.OrangeRed);
            _cts.Cancel();
        }
        if (!_cts.IsCancellationRequested)
            StartMailboxMonitorPolling();
        _runner = new SmtpTestRunner();
        _runner.SetMailboxFeedbackBaseline(
            _mailboxRunBaseline,
            (int)numMailboxStopPercent.Value,
            enabled: options.CapacityRun is { Enabled: true } && chkMailboxStopAtQuota.Checked);
        _runCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new Progress<ProgressUpdate>(UpdateProgress);

        try
        {
            var runStartedUtc = DateTimeOffset.UtcNow;
            // Do not pass the run token to Task.Run itself: even if the baseline quota
            // check cancelled before scheduling, the runner must execute its cleanup/result path.
            var result = await Task.Run(async () =>
                await _runner.RunAsync(options, progress, _cts.Token), CancellationToken.None);

            // Capture a final mailbox snapshot after any in-flight periodic poll completes.
            // This is observational evidence only; concurrent mailbox activity can affect deltas.
            var finalMailboxSnapshot = options.RequireTor ? null : await CaptureFinalMailboxSnapshotAsync();
            result = result with
            {
                MailboxRunDelta = MailboxMonitorMath.CalculateRunDelta(_mailboxRunBaseline, finalMailboxSnapshot)
            };

            var runReport = RunReportBuilder.Create(
                result.RunId ?? Guid.NewGuid().ToString("N"),
                runStartedUtc,
                DateTimeOffset.UtcNow,
                options,
                result,
                result.EndpointHealth);
            _lastRunReport = runReport;
            btnExportReport.Enabled = true;

            progressBar.Value = result.Requested > 0
                ? Math.Clamp((int)Math.Round((result.Sent + result.Failed) * 100d / result.Requested), 0, 100)
                : 0;
            lblStatus.Text = result.Cancelled
                ? (_mailboxQuotaStopTriggered
                    ? "Zastaveno: limit kvóty"
                    : !string.IsNullOrWhiteSpace(result.CapacityStopReason)
                        ? "Zastaveno: kapacitní limit"
                        : "Zastaveno uživatelem")
                : "Dokončeno";
            lblEta.Text = "";
            lblSentFailed.Text = $"OK:{result.Sent} FAIL:{result.Failed} Retry:{result.Retries}";

            AppendLog($"\n=== VÝSLEDEK ===", Color.Cyan);
            AppendLog($"Odesláno: {result.Sent}, Selhalo: {result.Failed}, Zrušeno: {result.Cancelled}", result.Failed > 0 ? Color.OrangeRed : Color.LightGreen);
            AppendLog($"Trvání: {result.Elapsed:hh\\:mm\\:ss\\.fff}, Throughput: {result.ThroughputPerSec:F2} msg/s", Color.LightGray);
            AppendLog($"SMTP SEND volání: {result.SmtpSendAttempts:N0}" +
                (result.CapacitySendReservations.HasValue ? $"; kapacitní rezervace: {result.CapacitySendReservations.Value:N0}; rezervovaný MIME objem {FormatMailboxBytes(result.CapacityMimeBytesReserved)}." : "."), Color.LightBlue);
            if (result.MailboxRunDelta is { } mailboxDelta)
            {
                AppendLog($"IMAP změna během běhu: quota zprávy {FormatDelta(mailboxDelta.QuotaMessageCountDelta)}; quota objem {FormatByteDelta(mailboxDelta.QuotaStorageBytesDelta)}; INBOX {FormatDelta(mailboxDelta.InboxMessageCountDelta)}; Junk/Spam {FormatDelta(mailboxDelta.JunkMessageCountDelta)}.", Color.LightBlue);
                AppendLog(mailboxDelta.Note, Color.DarkGray);
            }
            else if (IsMailboxMonitorConfigured())
                AppendLog("IMAP změnu během běhu nelze vyčíslit: chybí platný počáteční nebo konečný snímek či podporované metriky.", Color.Orange);
            AppendLog(RunObservability.FormatSummaryLine(RunObservability.FromReport(runReport)), Color.LightBlue);
            if (result.LatencyHistogram is { TotalCount: > 0 } histogram)
            {
                var buckets = string.Join(", ", histogram.Buckets.Where(b => b.Count > 0).Select(b => $"{b.Label}:{b.Count}"));
                AppendLog($"Rozložení latence: {buckets}", Color.LightGray);
            }
            if (result.EndpointHealth is { Count: > 0 } endpointSnapshots)
            {
                foreach (var endpoint in endpointSnapshots)
                    AppendLog($"Endpoint {endpoint.Key}: {endpoint.State}; success={endpoint.TotalSuccesses}; failures={endpoint.TotalFailures}; last={endpoint.LastFailureCategory ?? "—"}", endpoint.State == EndpointHealthState.Healthy ? Color.LightGreen : Color.Orange);
            }
            if (result.AdaptiveConcurrency > 0)
                AppendLog($"Adaptive concurrency: {result.AdaptiveConcurrency}", Color.LightBlue);
            if (result.CircuitBreakerOpen)
                AppendLog("Circuit breaker: OPEN", Color.Red);
            if (result.MxHosts?.Count > 0)
                AppendLog($"MX hosts: {string.Join(", ", result.MxHosts)}", Color.LightBlue);
            if (!string.IsNullOrEmpty(result.LastError))
                AppendLog($"Poslední chyba: {result.LastError}", Color.Red);

            // Webhook
            if (!string.IsNullOrWhiteSpace(txtWebhookUrl.Text))
            {
                try
                {
                    await WebhookNotifier.NotifyAsync(txtWebhookUrl.Text, result, _cts?.Token ?? CancellationToken.None);
                    AppendLog("Webhook notifikace odeslána.", Color.LightGreen);
                }
                catch (OperationCanceledException)
                {
                    AppendLog("Webhook přeskočen (test byl zrušen/okno se zavírá).", Color.Orange);
                }
                catch (Exception ex)
                {
                    AppendLog($"Webhook selhal: {ex.Message}", Color.Orange);
                }
            }
        }
        catch (OperationCanceledException)
        {
            lblStatus.Text = "Zrušeno";
            AppendLog("Test zrušen uživatelem.", Color.Yellow);
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Chyba";
            AppendLog($"Kritická chyba: {ex.Message}", Color.Red);
        }
        finally
        {
            StopMailboxMonitorPolling();
            _isRunning = false;
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            _cts?.Dispose();
            _cts = null;
            _runCompletion?.TrySetResult(true);
        }
    }

    private void OnStop()
    {
        if (_cts is null || _cts.IsCancellationRequested) return;
        _cts.Cancel();
        lblStatus.Text = "Zastavuji…";
    }

    private async Task OnSendTestMessageAsync()
    {
        if (_isRunning || _testConnectionCts != null)
            return;

        MailTestOptions options;
        try
        {
            var configured = BuildOptions();
            if (configured.Recipients.Count == 0)
                throw new InvalidOperationException("Nejdříve zadej alespoň jednoho příjemce na kartě Zpráva.");
            EnsureRealSmtpConfiguration(configured);

            // This is a single-message diagnostic, not a load test. Keep the selected
            // SMTP transport/auth/proxy settings, but remove batch, retry, attachment,
            // multi-recipient and automation side effects.
            options = configured with
            {
                Recipients = new[] { configured.Recipients[0] },
                CcRecipients = Array.Empty<string>(),
                BccRecipients = Array.Empty<string>(),
                MessageCount = 1,
                MaxConcurrency = 1,
                IntervalMs = 0,
                BatchMode = false,
                MaxRetries = 0,
                DryRun = false,
                Attachments = Array.Empty<string>(),
                InlineAttachments = Array.Empty<string>(),
                RandomTestData = false,
                UseBogusData = false,
                GenerateRandomHtml = false,
                GenerateRandomAttachments = false,
                VarySubjectBodyPerMessage = false,
                AutoRestartOnFailure = false,
                UseAdaptiveConcurrency = false,
                UseCircuitBreaker = false,
                EnableDashboard = false,
                EnableWarmup = false,
                EnableJitter = false,
                EnableBurstMode = false,
                EnableProgressiveBackoff = false,
                EnablePerRecipientLimit = false,
                EnableSendingTimeWindow = false,
                DetectGreylist = false,
                CheckRblBeforeStart = false,
                CollectObservedResponses = true,
                WebhookUrl = string.Empty,
                CapacityRun = null,
                DurationSeconds = 0,
                EmlTemplatePath = null,
                Accounts = null,
                DirectMxDelivery = false
            };
            Validation.Validate(options);
        }
        catch (Exception ex)
        {
            ShowErrorWithCopy("Testovací e-mail nelze připravit:\n" + ex.Message, "Kontrola nastavení", ex.ToString(), GuiErrorCodes.TestMessagePreparation);
            return;
        }

        var recipient = options.Recipients[0];
        var confirm = MessageBox.Show(
            $"Bude odeslána JEDNA skutečná e-mailová zpráva.\n\n" +
            $"SMTP: {options.SmtpHost}:{options.Port} ({options.Security})\n" +
            $"Od: {options.From}\nKomu: {recipient}\n\n" +
            "Zátěžový test, opakování, přílohy a další příjemci jsou vypnuté.\n" +
            "Dry-run se pro tuto jednorázovou kontrolu vypne a zpráva bude skutečně odeslána. Pokračovat?",
            "Potvrdit odeslání testovací zprávy",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
            return;

        _isRunning = true;
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        btnSendTest.Enabled = false;
        btnTestConnection.Enabled = false;
        _activeRunMessageCount = 1;
        _lastRunReport = null;
        btnExportReport.Enabled = false;
        rtbLog.Clear();
        smtpLogTab?.ClearLog();
        runMetricsChart?.ClearSamples();
        adaptiveControlChart?.ClearSamples();
        progressBar.Value = 0;
        lblStatus.Text = "Odesílám jednu testovací zprávu…";
        lblEta.Text = string.Empty;
        lblSentFailed.Text = "OK:0 FAIL:0";
        AppendLog("=== JEDNORÁZOVÝ TEST SMTP ODESLÁNÍ ===", Color.Cyan);
        AppendLog($"Cíl: {options.SmtpHost}:{options.Port} · příjemce: {recipient}", Color.LightGray);
        AppendLog("Režim: jedna skutečná zpráva, bez retry a bez zátěžového paralelismu.", Color.LightGreen);

        _cts = new CancellationTokenSource();
        _runner = new SmtpTestRunner();
        _runCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new Progress<ProgressUpdate>(UpdateProgress);

        try
        {
            var result = await Task.Run(
                async () => await _runner.RunAsync(options, progress, _cts.Token),
                CancellationToken.None);

            lblSentFailed.Text = $"OK:{result.Sent} FAIL:{result.Failed}";
            AppendLog(result.Sent == 1
                ? "SMTP server přijal testovací zprávu (2xx). To samo o sobě nepotvrzuje doručení do Inboxu."
                : $"Testovací odeslání selhalo: {result.LastError}", result.Sent == 1 ? Color.LightGreen : Color.Red);

            MessageBox.Show(
                result.Sent == 1
                    ? $"SMTP server zprávu přijal.\nPříjemce: {recipient}\nDoba: {result.AvgSmtpSendMs:F0} ms\n\nZkontroluj také složku Spam/Junk."
                    : $"SMTP server zprávu nepřijal.\n\n{result.LastError}",
                "Výsledek testovacího odeslání",
                MessageBoxButtons.OK,
                result.Sent == 1 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Testovací odeslání bylo zrušeno.", Color.Orange);
        }
        catch (Exception ex)
        {
            AppendLog($"Testovací odeslání skončilo chybou: {Validation.ExplainSmtpError(ex)}", Color.Red);
            ShowErrorWithCopy(ex.Message, "SMTP odeslání selhalo", ex.ToString(), GuiErrorCodes.SmtpSend);
        }
        finally
        {
            _isRunning = false;
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnSendTest.Enabled = true;
            btnTestConnection.Enabled = true;
            _cts?.Dispose();
            _cts = null;
            _runCompletion?.TrySetResult(true);
        }
    }

    private async Task OnTestConnectionAsync()
    {
        if (_testConnectionCts != null || _isRunning) return;

        using var cts = new CancellationTokenSource();
        _testConnectionCts = cts;
        _testConnectionCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        btnTestConnection.Enabled = false;
        lblStatus.Text = "Testuji SMTP…";
        try
        {
            var options = BuildOptions();
            Validation.Validate(options);
            EnsureRealSmtpConfiguration(options);
            var report = await TransportDiagnostics.RunAsync(
                options,
                new TransportDiagnosticOptions(
                    CheckDnsPolicy: true,
                    CheckMx: false,
                    CheckSmtp: true,
                    TryAuthenticate: options.UseAuthentication,
                    DryRun: options.DryRun),
                cts.Token);
            var result = report.Summary + (string.IsNullOrEmpty(report.Error) ? string.Empty : Environment.NewLine + Environment.NewLine + report.Error);
            if (!cts.IsCancellationRequested && !_closeApproved && !IsDisposed)
            {
                if (!string.IsNullOrEmpty(report.Error) && report.FailureKind.HasValue)
                {
                    var details = string.Join(Environment.NewLine, report.Steps);
                    ShowErrorWithCopy(result, "SMTP / diagnostika", details,
                        GuiErrorCodes.ForTransportFailure(report.FailureKind.Value));
                }
                else
                {
                    MessageBox.Show(result, "SMTP / diagnostika", MessageBoxButtons.OK,
                        report.Connected || options.DryRun ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Closing the form or an explicit cancellation is an expected outcome.
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested && !_closeApproved && !IsDisposed)
                ShowErrorWithCopy("Chyba při testu:\n" + ex.Message, "SMTP Test selhal", ex.ToString(),
                    GuiErrorCodes.ForTransportFailure(TransportFailureClassifier.Classify(ex)));
        }
        finally
        {
            _testConnectionCts = null;
            _testConnectionCompletion?.TrySetResult(true);
            _testConnectionCompletion = null;
            if (!IsDisposed && !_closeApproved)
            {
                btnTestConnection.Enabled = true;
                lblStatus.Text = "Připraveno";
            }
        }
    }
}
