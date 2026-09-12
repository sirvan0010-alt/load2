namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private void UpdateSafetyBanner()
    {
        if (lblSafetyBanner is null || pnlSafetyBanner is null) return;
        var testMode = chkTestMode?.Checked == true;
        if (testMode)
        {
            lblSafetyBanner.Text = "TEST MODE — odesílání jen na povolené domény. Pro první pokus: 1 zpráva, StartTls, ověřený SMTP.";
            pnlSafetyBanner.BackColor = Color.FromArgb(220, 245, 220);
            lblSafetyBanner.ForeColor = Color.FromArgb(20, 80, 20);
        }
        else
        {
            lblSafetyBanner.Text = "OSTRE ODESÍLÁNÍ — Test mode je vypnutý. Zprávy mohou jít kamkoli podle nastavení. Zapni Test mode, pokud jen zkoušíš.";
            pnlSafetyBanner.BackColor = Color.FromArgb(255, 230, 180);
            lblSafetyBanner.ForeColor = Color.FromArgb(120, 60, 0);
        }
        if (_darkMode)
        {
            if (testMode)
            {
                pnlSafetyBanner.BackColor = Color.FromArgb(30, 70, 40);
                lblSafetyBanner.ForeColor = Color.FromArgb(180, 230, 180);
            }
            else
            {
                pnlSafetyBanner.BackColor = Color.FromArgb(90, 60, 20);
                lblSafetyBanner.ForeColor = Color.FromArgb(255, 210, 140);
            }
        }
    }

    private void ShowGettingStartedWizard()
    {
        const string text =
            "JAK ZAČÍT (5 kroků)\n\n" +
            "1) Záložka Test — zapni Test mode a vyplň Allowed domains (např. example.com).\n" +
            "2) Záložka SMTP Server — host, port 587, StartTls, případně jméno a heslo.\n" +
            "3) Záložka Zpráva — From / To (To musí být na povolené doméně), krátký předmět.\n" +
            "4) Počet zpráv = 1, paralelismus = 1. Volitelně Test spojení.\n" +
            "5) START. Sleduj log dole a záložku SMTP log.\n\n" +
            "TIPY\n" +
            "• Tooltip = najeď myší na popisek pole.\n" +
            "• Oranžový banner = Test mode vypnutý (ostré odesílání).\n" +
            "• Zelený banner = Test mode (bezpečnější).\n" +
            "• Proxy / IPv6 / circuit nech vypnuté, dokud základní test neprojde.\n" +
            "• Auto-restart může poslat test znovu — nech vypnutý, dokud nevíš proč ho chceš.";
        MessageBox.Show(text, "MailLoadTester — jak začít", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ApplyTheme(bool dark)
    {
        _darkMode = dark;
        var bg = dark ? Color.FromArgb(32, 32, 36) : SystemColors.Control;
        var fg = dark ? Color.FromArgb(230, 230, 230) : SystemColors.ControlText;
        var inputBg = dark ? Color.FromArgb(45, 45, 50) : SystemColors.Window;
        var inputFg = dark ? Color.FromArgb(230, 230, 230) : SystemColors.WindowText;
        BackColor = bg;
        ForeColor = fg;
        ApplyThemeRecursive(this, dark, bg, fg, inputBg, inputFg);
        btnStart.BackColor = Color.FromArgb(34, 139, 34);
        btnStart.ForeColor = Color.White;
        btnStop.BackColor = Color.FromArgb(178, 34, 34);
        btnStop.ForeColor = Color.White;
        UpdateSafetyBanner();
    }

    private static void ApplyThemeRecursive(Control root, bool dark, Color bg, Color fg, Color inputBg, Color inputFg)
    {
        foreach (Control c in root.Controls)
        {
            switch (c)
            {
                case TextBox or RichTextBox or ListBox or NumericUpDown or ComboBox or DataGridView:
                    c.BackColor = inputBg;
                    c.ForeColor = inputFg;
                    break;
                case Button btn when btn.Text is "▶ START" or "■ STOP":
                    break;
                case Button:
                    c.BackColor = dark ? Color.FromArgb(55, 55, 62) : SystemColors.Control;
                    c.ForeColor = fg;
                    break;
                case TabPage or GroupBox or Panel or FlowLayoutPanel or TableLayoutPanel or TabControl:
                    if (c is not StatusStrip)
                    {
                        c.BackColor = bg;
                        c.ForeColor = fg;
                    }
                    break;
                case Label or CheckBox:
                    c.ForeColor = fg;
                    if (c is CheckBox)
                        c.BackColor = bg;
                    break;
                case StatusStrip ss:
                    ss.BackColor = dark ? Color.FromArgb(28, 28, 32) : SystemColors.Control;
                    ss.ForeColor = fg;
                    break;
            }
            if (c.HasChildren)
                ApplyThemeRecursive(c, dark, bg, fg, inputBg, inputFg);
        }
    }

}
