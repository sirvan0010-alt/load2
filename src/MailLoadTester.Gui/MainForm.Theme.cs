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
            lblSafetyBanner.Text = "OSTRÉ ODESÍLÁNÍ — Test mode je vypnutý. Zprávy mohou jít kamkoli podle nastavení. Zapni Test mode, pokud jen zkoušíš.";
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
            "4) Nejdřív použij Odeslat 1 testovací e-mail — po potvrzení odešle jednu skutečnou zprávu.\n" +
            "5) Teprve po úspěšném doručení spusť START s počtem 1 a paralelismem 1.\n" +
            "6) Aplikační log a SMTP události najdeš dole vedle sebe.\n\n" +
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
        var bg = dark ? Color.FromArgb(25, 30, 38) : Color.FromArgb(242, 245, 249);
        var fg = dark ? Color.FromArgb(229, 234, 242) : Color.FromArgb(35, 45, 60);
        var inputBg = dark ? Color.FromArgb(35, 42, 52) : Color.White;
        var inputFg = dark ? Color.FromArgb(235, 239, 245) : Color.FromArgb(30, 41, 59);
        BackColor = bg;
        ForeColor = fg;
        ApplyThemeRecursive(this, dark, bg, fg, inputBg, inputFg);
        ApplyModernControlStyle(this, dark);
        btnStart.BackColor = Color.FromArgb(34, 139, 34);
        btnStart.ForeColor = Color.White;
        btnStop.BackColor = Color.FromArgb(178, 34, 34);
        btnStop.ForeColor = Color.White;
        UpdateSafetyBanner();
    }


    private static void ApplyModernControlStyle(Control root, bool dark)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.Cursor = Cursors.Hand;
                    button.Font = new Font("Segoe UI", 9.5F, FontStyle.Semibold);
                    button.Padding = new Padding(8, 3, 8, 3);
                    break;
                case GroupBox group:
                    group.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    group.Padding = new Padding(10, 8, 10, 8);
                    break;
                case TextBox or ComboBox or NumericUpDown:
                    control.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
                    break;
                case DataGridView grid:
                    grid.BorderStyle = BorderStyle.None;
                    grid.EnableHeadersVisualStyles = false;
                    grid.ColumnHeadersHeight = 34;
                    grid.RowTemplate.Height = 30;
                    grid.BackgroundColor = dark ? Color.FromArgb(35, 42, 52) : Color.White;
                    grid.GridColor = dark ? Color.FromArgb(65, 74, 86) : Color.FromArgb(226, 232, 240);
                    grid.ColumnHeadersDefaultCellStyle.BackColor = dark
                        ? Color.FromArgb(47, 57, 70) : Color.FromArgb(232, 238, 246);
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = dark
                        ? Color.FromArgb(235, 239, 245) : Color.FromArgb(40, 52, 70);
                    grid.DefaultCellStyle.SelectionBackColor = dark
                        ? Color.FromArgb(48, 87, 130) : Color.FromArgb(213, 230, 249);
                    grid.DefaultCellStyle.SelectionForeColor = dark ? Color.White : Color.FromArgb(25, 40, 60);
                    break;
            }

            if (control.HasChildren)
                ApplyModernControlStyle(control, dark);
        }
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
                case TabPage or GroupBox or Panel or FlowLayoutPanel or TableLayoutPanel or TabControl or SplitContainer or UserControl:
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
