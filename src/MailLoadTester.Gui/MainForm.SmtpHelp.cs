namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private SmtpProviderProfile? GetDetectedSmtpProvider() =>
        SmtpProviderProfiles.Detect(txtHost?.Text ?? string.Empty, txtUser?.Text ?? string.Empty);

    private void UpdateSmtpProviderGuidance()
    {
        if (cmbSmtpProvider is null || lblSmtpProviderGuidance is null)
            return;

        var profile = GetDetectedSmtpProvider();
        if (profile is null)
        {
            if (string.IsNullOrWhiteSpace(txtHost.Text) && string.IsNullOrWhiteSpace(txtUser.Text))
                lblSmtpProviderGuidance.Text = "Zadej e-mail nebo SMTP server. Poskytovatele rozpoznám automaticky a zobrazím správný návod.";
            else
                lblSmtpProviderGuidance.Text = "Vlastní nebo nerozpoznaný SMTP server. Ověř host, port, TLS a způsob autentizace u poskytovatele.";
            cmbSmtpProvider.SelectedIndex = -1;
            return;
        }

        var index = SmtpProviderProfiles.All.ToList().FindIndex(p => p.Id == profile.Id);
        cmbSmtpProvider.SelectedIndex = index;
        lblSmtpProviderGuidance.Text = profile.Guidance;
    }

    private string GetSmtpPasswordForSubmission()
    {
        var password = txtPass.Text;
        // Google displays App Passwords in four-character groups. Remove copied
        // formatting spaces before SMTP AUTH, without changing other providers' secrets.
        if (GetDetectedSmtpProvider()?.Id == "Gmail"
            && !string.Equals(cmbAuthMethod.SelectedItem?.ToString(), "OAuth2", StringComparison.OrdinalIgnoreCase))
            return string.Concat(password.Where(c => !char.IsWhiteSpace(c)));

        return password;
    }

    private void ShowSmtpCredentialHelp()
    {
        var profile = GetDetectedSmtpProvider();
        var providerId = profile?.Id ?? string.Empty;
        string title;
        string instructions;
        string url;

        switch (providerId)
        {
            case "Gmail":
                title = "Google / Gmail – přístup MailLoadTesteru";
                instructions =
                    "1. Použij celou adresu Gmailu v poli Uživatel.\n" +
                    "2. Google účet musí mít zapnuté dvoufázové ověření.\n" +
                    "3. Otevři Hesla aplikací, vytvoř heslo s názvem MailLoadTester a zkopíruj zobrazených 16 znaků do pole Heslo.\n" +
                    "4. Pokud se heslo zobrazuje po skupinách, můžeš jej vložit i s mezerami – MailLoadTester je před SMTP přihlášením odstraní.\n\n" +
                    "Hlavní heslo Google sem nikdy nezadávej. Heslo aplikace není hlavní heslo a můžeš ho kdykoli odvolat. Google ho může zneplatnit také po změně hlavního hesla.\n\n" +
                    "Automatické přihlášení přes Google (OAuth) zatím aplikace neumí. Vyžaduje vlastní registraci desktopové aplikace u Google a implementaci bezpečného OAuth/XOAUTH2 toku; pouhé otevření Google účtu by token bezpečně nezískalo.";
                url = "https://myaccount.google.com/apppasswords";
                break;
            case "Seznam":
                title = "Seznam.cz / Email.cz – heslo pro poštovní program";
                instructions =
                    "Použij celou e-mailovou adresu. Pokud máš zapnuté dvoufázové ověření, vytvoř v nastavení účtu zvláštní heslo pro poštovní programy a vlož ho sem. Bez dvoufázového ověření se obvykle používá běžné heslo účtu.\n\n" +
                    "SMTP: smtp.seznam.cz, port 465, SSL/TLS.";
                url = "https://ucet.seznam.cz/";
                break;
            case "Yahoo":
                title = "Yahoo Mail – heslo aplikace";
                instructions = "Vygeneruj samostatné heslo pro aplikaci třetí strany a použij celé Yahoo přihlašovací jméno. Běžné heslo nemusí SMTP přihlášení povolit.";
                url = "https://login.yahoo.com/account/security";
                break;
            case "iCloud":
                title = "Apple iCloud Mail – heslo pro konkrétní aplikaci";
                instructions = "Na Apple účtu vytvoř heslo pro konkrétní aplikaci. Použij celou iCloud e-mailovou adresu; hlavní heslo Apple účtu do MailLoadTesteru nezadávej.";
                url = "https://account.apple.com/";
                break;
            case "Fastmail":
                title = "Fastmail – heslo aplikace";
                instructions = "Ve webovém nastavení Fastmail vytvoř aplikační heslo pro MailLoadTester a použij SMTP server smtp.fastmail.com.";
                url = "https://www.fastmail.help/hc/en-us/articles/1500000278342-App-passwords";
                break;
            case "Zoho":
            case "ZohoEU":
                title = "Zoho Mail – heslo aplikace";
                instructions = "Pokud máš zapnuté vícefaktorové ověření, vytvoř aplikační heslo. U firemních/doménových schránek ověř správný SMTP server a datacentrum v administraci Zoho.";
                url = "https://accounts.zoho.com/home#security/app-passwords";
                break;
            case "Microsoft365":
            case "Outlook":
                title = "Microsoft – SMTP autentizace";
                instructions = "Microsoft doporučuje OAuth 2.0. Dostupnost SMTP AUTH a případných hesel aplikací závisí na typu účtu a zásadách organizace. MailLoadTester zatím neumí automaticky získat OAuth token; do pole Heslo nelze vložit běžné heslo Microsoft účtu, pokud server vyžaduje OAuth.";
                url = "https://learn.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth";
                break;
            default:
                title = "SMTP přihlašovací údaje";
                instructions = "Použij přihlašovací údaje a způsob autentizace, které výslovně podporuje tvůj poskytovatel SMTP. Pokud poskytovatel nabízí hesla aplikací nebo OAuth 2.0, upřednostni je před hlavním heslem účtu.";
                url = "https://support.google.com/mail/answer/7126229";
                break;
        }

        var result = MessageBox.Show(
            instructions + "\n\nOtevřít oficiální stránku poskytovatele v prohlížeči?",
            title,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result != DialogResult.Yes)
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Stránku se nepodařilo otevřít: " + ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
