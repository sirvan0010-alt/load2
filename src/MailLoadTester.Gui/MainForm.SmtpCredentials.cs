namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private void LoadRememberedSmtpCredentials()
    {
        try
        {
            var credentials = new SmtpCredentialStore().Load();
            if (credentials is null) return;

            _loadingSmtpCredentials = true;
            try
            {
                txtHost.Text = credentials.Host;
                numPort.Value = Math.Clamp(credentials.Port, (int)numPort.Minimum, (int)numPort.Maximum);
                txtUser.Text = credentials.Username;
                txtPass.Text = credentials.Password;
                chkAuth.Checked = true;
                cmbSecurity.SelectedItem = credentials.Security;
                cmbAuthMethod.SelectedItem = credentials.AuthMethod;
                chkRememberSmtpPassword.Checked = true;
            }
            finally
            {
                _loadingSmtpCredentials = false;
            }
        }
        catch (Exception ex)
        {
            try { new SmtpCredentialStore().Delete(); } catch { }
            lblStatus.Text = "Uložené SMTP přihlašovací údaje nelze načíst; byly odstraněny.";
            System.Diagnostics.Debug.WriteLine("SMTP credential restore failed: " + ex.Message);
        }
    }

    private async Task SaveRememberedSmtpCredentialsAsync(MailTestOptions options)
    {
        if (!chkRememberSmtpPassword.Checked || !options.UseAuthentication ||
            string.IsNullOrWhiteSpace(options.Password) ||
            string.IsNullOrWhiteSpace(options.Username))
            return;

        try
        {
            await new SmtpCredentialStore().SaveAsync(new SmtpCredentialStore.Credentials(
                options.SmtpHost,
                options.Port,
                options.Username,
                options.Password,
                options.Security.ToString(),
                options.AuthMethod.ToString()));
            if (!IsDisposed)
                lblStatus.Text = "SMTP přihlašovací údaje bezpečně zapamatovány pro tento účet Windows.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                lblStatus.Text = "SMTP údaje se nepodařilo uložit: " + ex.Message;
        }
    }

    private void DeleteRememberedSmtpCredentials()
    {
        try
        {
            new SmtpCredentialStore().Delete();
            if (!IsDisposed && lblStatus is not null)
                lblStatus.Text = "Uložené SMTP heslo bylo odstraněno.";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && lblStatus is not null)
                lblStatus.Text = "SMTP heslo se nepodařilo odstranit: " + ex.Message;
        }
    }
}
