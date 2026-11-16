namespace MailLoadTester.Gui;

public sealed partial class MainForm
{

    private async Task OnSaveProfileAsync()
    {
        try
        {
            var options = BuildOptions();
            Validation.Validate(options);
            using var dlg = new SaveFileDialog { Filter = "MailLoadTester profil|*.json", FileName = "profile.json" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            await ProfileStore.SaveAsync(dlg.FileName, options);
            lblStatus.Text = "Profil uložen";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Profil se nepodařilo uložit:\n{ex.Message}", "Uložení profilu", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnLoadProfileAsync()
    {
        try
        {
            using var dlg = new OpenFileDialog { Filter = "MailLoadTester profil|*.json|Všechny soubory|*.*" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            var profile = await ProfileStore.LoadAsync(dlg.FileName);
            var o = profile.Options;
            txtHost.Text = o.SmtpHost;
            var smtpProvider = SmtpProviderProfiles.FindByHost(o.SmtpHost);
            var smtpProviderIndex = smtpProvider is null
                ? -1
                : SmtpProviderProfiles.All.ToList().FindIndex(p => p.Id == smtpProvider.Id);
            cmbSmtpProvider.SelectedIndex = smtpProviderIndex;
            lblSmtpProviderGuidance.Text = smtpProvider?.Guidance
                ?? "Vlastní SMTP server. Zkontroluj host, port a zabezpečení podle dokumentace poskytovatele.";
            numPort.Value = Math.Clamp(o.Port, numPort.Minimum, numPort.Maximum);
            cmbSecurity.SelectedItem = o.Security.ToString();
            chkIgnoreCert.Checked = o.IgnoreCertificateErrors;
            chkAuth.Checked = o.UseAuthentication;
            cmbAuthMethod.SelectedItem = o.AuthMethod.ToString();
            txtUser.Text = o.Username;
            txtPass.Text = o.Password ?? string.Empty;
            _accountDrafts.Clear();
            if (o.Accounts is { Count: > 0 })
            {
                foreach (var account in o.Accounts)
                {
                    _accountDrafts.Add(new SmtpAccountDraft(
                        account.Id,
                        account.SmtpHost,
                        account.Port,
                        account.Security,
                        account.UseAuthentication,
                        account.Username,
                        account.Password,
                        Enabled: true,
                        AuthMethod: account.AuthMethod));
                }
            }
            RefreshAccountsListView();
            txtSourceIp.Text = o.SourceIp;
            txtIpv6Prefix.Text = o.Ipv6Prefix;
            numIpv6PrefixLen.Value = Math.Clamp(o.Ipv6PrefixLength, numIpv6PrefixLen.Minimum, numIpv6PrefixLen.Maximum);
            txtIpv4Rotation.Text = o.Ipv4Rotation;
            chkIpv4RotationRandom.Checked = o.Ipv4RotationRandom;
            txtClientCert.Text = o.ClientCertificatePath;
            txtClientCertPass.Text = o.ClientCertificatePassword;
            txtFrom.Text = o.From;
            txtTo.Text = string.Join(Environment.NewLine, o.Recipients);
            txtCc.Text = string.Join(",", o.CcRecipients ?? Array.Empty<string>());
            txtBcc.Text = string.Join(",", o.BccRecipients ?? Array.Empty<string>());
            txtDisplayName.Text = o.DisplayName;
            txtSubject.Text = o.Subject;
            txtBody.Text = o.Body;
            chkHtmlBody.Checked = o.HtmlBody;
            chkRandomData.Checked = o.RandomTestData;
            chkBogusData.Checked = o.UseBogusData;
            chkRandomHtml.Checked = o.GenerateRandomHtml;
            chkRandomAttachments.Checked = o.GenerateRandomAttachments;
            numMaxRandomAttachments.Value = Math.Clamp(o.MaxRandomAttachments, numMaxRandomAttachments.Minimum, numMaxRandomAttachments.Maximum);
            chkVaryMessage.Checked = o.VarySubjectBodyPerMessage;
            numRandomAttachmentSizeMb.Value = Math.Clamp(o.RandomAttachmentSizeMb, numRandomAttachmentSizeMb.Minimum, numRandomAttachmentSizeMb.Maximum);
            numCount.Value = Math.Clamp(o.MessageCount, numCount.Minimum, numCount.Maximum);
            numInterval.Value = Math.Clamp(o.IntervalMs, numInterval.Minimum, numInterval.Maximum);
            numConcurrency.Value = Math.Clamp(o.MaxConcurrency, numConcurrency.Minimum, numConcurrency.Maximum);
            chkBatchMode.Checked = o.BatchMode;
            numBatchSize.Value = Math.Clamp(o.BatchSize, numBatchSize.Minimum, numBatchSize.Maximum);
            numBatchPause.Value = Math.Clamp(o.BatchPauseSeconds, numBatchPause.Minimum, numBatchPause.Maximum);
            numRetries.Value = Math.Clamp(o.MaxRetries, numRetries.Minimum, numRetries.Maximum);
            chkCapacityRun.Checked = o.CapacityRun?.Enabled ?? false;
            numCapacityMaxMessages.Value = Math.Clamp(o.CapacityRun?.MaxMessageCount ?? 1000, numCapacityMaxMessages.Minimum, numCapacityMaxMessages.Maximum);
            numCapacityTargetMiB.Value = Math.Clamp((decimal)(o.CapacityRun?.TargetMimeBytes ?? 0) / (1024m * 1024m), numCapacityTargetMiB.Minimum, numCapacityTargetMiB.Maximum);
            numCapacityMaxMessageMiB.Value = Math.Clamp((decimal)(o.CapacityRun?.MaxMimeMessageBytes ?? 0) / (1024m * 1024m), numCapacityMaxMessageMiB.Minimum, numCapacityMaxMessageMiB.Maximum);
            numCapacityMaxPerMinute.Value = Math.Clamp(o.CapacityRun?.MaxMessagesPerMinute ?? 0, numCapacityMaxPerMinute.Minimum, numCapacityMaxPerMinute.Maximum);
            chkCapacityStartupValidation.Checked = o.CapacityRun?.ValidateBeforeRun ?? false;
            chkDryRun.Checked = o.DryRun;
            chkTestMode.Checked = o.TestMode;
            txtAllowedDomains.Text = o.AllowedDomains;
            chkSocks5.Checked = o.UseSocks5Proxy;
            chkRequireTor.Checked = o.RequireTor;
            txtProxyHost.Text = o.ProxyHost;
            if (txtProxyList != null) txtProxyList.Text = o.ProxyList ?? "";
            _idleHealthCheckSeconds = Math.Clamp(o.IdleConnectionHealthCheckSeconds, 0, 86_400);
            _circuitWindowSize = o.CircuitBreakerWindowSize > 0 ? o.CircuitBreakerWindowSize : 100;
            _circuitFailurePercent = o.CircuitBreakerFailurePercent > 0 ? o.CircuitBreakerFailurePercent : 90.0;
            if (chkProxyListRandom != null) chkProxyListRandom.Checked = o.ProxyListRandom;
            if (numProxyBanMinutes != null) numProxyBanMinutes.Value = Math.Clamp(o.ProxyBanMinutes <= 0 ? 15 : o.ProxyBanMinutes, 1, 1440);
            numProxyPort.Value = Math.Clamp(o.ProxyPort <= 0 ? 1080 : o.ProxyPort, numProxyPort.Minimum, numProxyPort.Maximum);
            txtProxyUser.Text = o.ProxyUsername;
            txtProxyPass.Text = o.ProxyPassword ?? string.Empty;
            chkJitter.Checked = o.EnableJitter;
            numJitterPercent.Value = Math.Clamp(o.JitterPercent, numJitterPercent.Minimum, numJitterPercent.Maximum);
            chkBurst.Checked = o.EnableBurstMode;
            numBurstSize.Value = Math.Clamp(o.BurstSize, numBurstSize.Minimum, numBurstSize.Maximum);
            numBurstPause.Value = Math.Clamp(o.BurstPauseSeconds, numBurstPause.Minimum, numBurstPause.Maximum);
            chkBackoff.Checked = o.EnableProgressiveBackoff;
            numBackoffAfter.Value = Math.Clamp(o.BackoffAfterSuccesses, numBackoffAfter.Minimum, numBackoffAfter.Maximum);
            chkGreylist.Checked = o.DetectGreylist;
            numGreylistMinutes.Value = Math.Clamp(o.GreylistRetryMinutes, numGreylistMinutes.Minimum, numGreylistMinutes.Maximum);
            chkWarmup.Checked = o.EnableWarmup;
            txtWarmupPhases.Text = o.WarmupPhases;
            chkTimeWindow.Checked = o.EnableSendingTimeWindow;
            numWindowFrom.Value = Math.Clamp(o.SendingWindowFromHour, numWindowFrom.Minimum, numWindowFrom.Maximum);
            numWindowTo.Value = Math.Clamp(o.SendingWindowToHour, numWindowTo.Minimum, numWindowTo.Maximum);
            chkPerRecipient.Checked = o.EnablePerRecipientLimit;
            numMaxPerRecipient.Value = Math.Clamp(o.MaxMessagesPerRecipient, numMaxPerRecipient.Minimum, numMaxPerRecipient.Maximum);
            chkRbl.Checked = o.CheckRblBeforeStart;
            chkCollectObserved.Checked = o.CollectObservedResponses;
            chkCircuit.Checked = o.UseCircuitBreaker;
            numCircuitThreshold.Value = Math.Clamp(o.CircuitBreakerThreshold, numCircuitThreshold.Minimum, numCircuitThreshold.Maximum);
            chkDirectMx.Checked = o.DirectMxDelivery;
            chkPreWarm.Checked = o.PreWarmConnections;
            chkAdaptive.Checked = o.UseAdaptiveConcurrency;
            UpdateRandomAttachmentSafetyInfo();
            UpdateSafetyBanner();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Profil se nepodařilo načíst:\n{ex.Message}", "Načtení profilu", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
