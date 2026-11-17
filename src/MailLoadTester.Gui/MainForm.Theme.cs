namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private void UpdateSafetyBanner()
    {
        if (lblSafetyBanner is null || pnlSafetyBanner is null) return;
        var testMode = chkTestMode?.Checked == true;
        var ignoreCertificateErrors = chkIgnoreCert?.Checked == true;

        if (ignoreCertificateErrors)
        {
            lblSafetyBanner.Text = "BEZPEČNOSTNÍ VAROVÁNÍ — Kontrola TLS certifikátu je vypnutá. Spojení může být zranitelné vůči útoku MITM. Používej pouze pro izolované testování.";
            pnlSafetyBanner.BackColor = Color.FromArgb(255, 205, 190);
            lblSafetyBanner.ForeColor = Color.FromArgb(130, 25, 15);
        }
        else if (testMode)
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
            if (ignoreCertificateErrors)
            {
                pnlSafetyBanner.BackColor = Color.FromArgb(105, 35, 25);
                lblSafetyBanner.ForeColor = Color.FromArgb(255, 205, 190);
            }
            else if (testMode)
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
        using var wizard = new Form
        {
            Text = "MailLoadTester — průvodce prvním testem",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(720, 520),
            BackColor = _darkMode ? Color.FromArgb(25, 30, 38) : Color.FromArgb(242, 245, 249),
            ForeColor = _darkMode ? Color.FromArgb(229, 234, 242) : Color.FromArgb(35, 45, 60)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(18), BackColor = wizard.BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        var intro = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Proveď základní nastavení v tomto pořadí. Každé tlačítko tě přenese přímo na příslušnou kartu.",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            ForeColor = wizard.ForeColor
        };
        layout.Controls.Add(intro, 0, 0);

        var steps = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(2)
        };
        layout.Controls.Add(steps, 0, 1);

        void AddStep(string number, string title, string description, string tabName, string buttonText)
        {
            var card = new Panel
            {
                Width = 650, Height = 92, Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(12), BorderStyle = BorderStyle.FixedSingle,
                BackColor = _darkMode ? Color.FromArgb(38, 46, 58) : Color.White
            };
            var heading = new Label
            {
                Left = 10, Top = 8, Width = 430, Height = 24,
                Text = number + "  " + title, Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = wizard.ForeColor
            };
            var detail = new Label
            {
                Left = 10, Top = 34, Width = 430, Height = 44,
                Text = description, Font = new Font("Segoe UI", 9F),
                ForeColor = wizard.ForeColor
            };
            var open = new Button
            {
                Text = buttonText, Width = 165, Height = 34,
                Left = 465, Top = 27, Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand
            };
            open.FlatAppearance.BorderSize = 0;
            open.Click += (_, _) =>
            {
                var page = tabs.TabPages.Cast<TabPage>().FirstOrDefault(p => p.Text.Equals(tabName, StringComparison.OrdinalIgnoreCase));
                if (page is not null) tabs.SelectedTab = page;
                wizard.Close();
            };
            card.Controls.Add(heading);
            card.Controls.Add(detail);
            card.Controls.Add(open);
            steps.Controls.Add(card);
        }

        AddStep("1", "SMTP připojení", "Vyplň server, port a TLS. Otestuj připojení; heslo lze volitelně zapamatovat přes DPAPI.", "SMTP Server", "Otevřít SMTP");
        AddStep("2", "Zpráva a příjemce", "Nastav From, To, předmět a krátké tělo. Začni s jedním autorizovaným příjemcem.", "E-mail (Od / obsah)", "Otevřít zprávu");
        AddStep("3", "Bezpečný test", "Zapni Test mode, nastav povolenou doménu, 1 zprávu a paralelismus 1. Dry-run nejprve ověří konfiguraci.", "Test", "Otevřít test");
        AddStep("4", "Pokročilé funkce", "Proxy, rotace IP, pacing a další limity nech vypnuté, dokud základní test nefunguje.", "Proxy", "Otevřít Proxy");

        var footer = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Upozornění: Test Mode omezuje cílové domény, ale není totéž co Dry-run.",
            ForeColor = _darkMode ? Color.FromArgb(255, 210, 140) : Color.FromArgb(120, 60, 0)
        };
        layout.Controls.Add(footer, 0, 2);
        wizard.Controls.Add(layout);
        wizard.ShowDialog(this);
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
                    button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
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
