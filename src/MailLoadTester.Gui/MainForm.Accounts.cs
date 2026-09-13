namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private readonly List<SmtpAccountDraft> _accountDrafts = new();
    private ListView lvAccounts = null!;
    private Button btnAccountAdd = null!, btnAccountEdit = null!, btnAccountRemove = null!, btnAccountToggle = null!;

    private void InitAccountsTab(TabControl tabs)
    {
        var tab = new TabPage("Účty SMTP") { Padding = new Padding(12) };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        lvAccounts = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false
        };
        lvAccounts.Columns.Add("Id", 100);
        lvAccounts.Columns.Add("Host", 160);
        lvAccounts.Columns.Add("Port", 50);
        lvAccounts.Columns.Add("TLS", 80);
        lvAccounts.Columns.Add("User", 120);
        lvAccounts.Columns.Add("Enabled", 70);
        lvAccounts.Columns.Add("Auth", 50);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        btnAccountAdd = new Button { Text = "Přidat", Width = 90, Height = 30 };
        btnAccountEdit = new Button { Text = "Upravit", Width = 90, Height = 30 };
        btnAccountRemove = new Button { Text = "Odebrat", Width = 90, Height = 30 };
        btnAccountToggle = new Button { Text = "Zap/Vyp", Width = 90, Height = 30 };
        btnAccountAdd.Click += (_, _) => AccountAdd();
        btnAccountEdit.Click += (_, _) => AccountEdit();
        btnAccountRemove.Click += (_, _) => AccountRemove();
        btnAccountToggle.Click += (_, _) => AccountToggle();
        buttons.Controls.AddRange(new Control[] { btnAccountAdd, btnAccountEdit, btnAccountRemove, btnAccountToggle });

        root.Controls.Add(lvAccounts, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        tab.Controls.Add(root);
        tabs.TabPages.Add(tab);
        RefreshAccountsListView();
    }

    private void RefreshAccountsListView()
    {
        if (lvAccounts is null) return;
        lvAccounts.BeginUpdate();
        lvAccounts.Items.Clear();
        foreach (var d in _accountDrafts)
        {
            var item = new ListViewItem(d.Id);
            item.SubItems.Add(d.SmtpHost);
            item.SubItems.Add(d.Port.ToString());
            item.SubItems.Add(d.Security.ToString());
            item.SubItems.Add(d.Username);
            item.SubItems.Add(d.Enabled ? "Ano" : "Ne");
            item.SubItems.Add(d.UseAuthentication ? "Ano" : "Ne");
            item.Tag = d;
            if (!d.Enabled)
                item.ForeColor = Color.Gray;
            lvAccounts.Items.Add(item);
        }
        lvAccounts.EndUpdate();
    }

    private IReadOnlyList<SmtpAccount>? BuildAccountsFromUi()
    {
        SmtpAccountMapping.ValidateAll(_accountDrafts);
        return SmtpAccountMapping.ToOptionsAccounts(_accountDrafts);
    }

    private void AccountAdd()
    {
        if (!TryEditAccount(null, out var draft)) return;
        try
        {
            var ids = new HashSet<string>(_accountDrafts.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
            SmtpAccountMapping.ValidateDraft(draft, ids);
            _accountDrafts.Add(draft);
            RefreshAccountsListView();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Účet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AccountEdit()
    {
        if (lvAccounts.SelectedItems.Count == 0) return;
        var current = (SmtpAccountDraft)lvAccounts.SelectedItems[0].Tag!;
        if (!TryEditAccount(current, out var draft)) return;
        try
        {
            var ids = new HashSet<string>(
                _accountDrafts.Where(a => !ReferenceEquals(a, current)).Select(a => a.Id),
                StringComparer.OrdinalIgnoreCase);
            SmtpAccountMapping.ValidateDraft(draft, ids);
            var idx = _accountDrafts.IndexOf(current);
            if (idx >= 0) _accountDrafts[idx] = draft;
            RefreshAccountsListView();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Účet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AccountRemove()
    {
        if (lvAccounts.SelectedItems.Count == 0) return;
        var current = (SmtpAccountDraft)lvAccounts.SelectedItems[0].Tag!;
        _accountDrafts.Remove(current);
        RefreshAccountsListView();
    }

    private void AccountToggle()
    {
        if (lvAccounts.SelectedItems.Count == 0) return;
        var current = (SmtpAccountDraft)lvAccounts.SelectedItems[0].Tag!;
        var idx = _accountDrafts.IndexOf(current);
        if (idx < 0) return;
        _accountDrafts[idx] = current with { Enabled = !current.Enabled };
        RefreshAccountsListView();
    }

    private bool TryEditAccount(SmtpAccountDraft? existing, out SmtpAccountDraft draft)
    {
        draft = existing ?? new SmtpAccountDraft(
            "account1", "smtp.example.com", 587, SmtpSecurity.StartTls, true, "", "", true);

        using var dlg = new Form
        {
            Text = existing is null ? "Přidat SMTP účet" : "Upravit SMTP účet",
            Width = 420,
            Height = 360,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(12),
            WrapContents = false,
            AutoScroll = true
        };

        TextBox Txt(string label, string value, bool password = false)
        {
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            var t = new TextBox { Width = 360, Text = value };
            if (password) t.PasswordChar = '●';
            panel.Controls.Add(t);
            return t;
        }

        var txtId = Txt("Id:", draft.Id);
        var txtHost = Txt("Host:", draft.SmtpHost);
        panel.Controls.Add(new Label { Text = "Port:", AutoSize = true });
        var numPort = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = Math.Clamp(draft.Port, 1, 65535), Width = 120 };
        panel.Controls.Add(numPort);
        panel.Controls.Add(new Label { Text = "TLS:", AutoSize = true });
        var cmbTls = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        cmbTls.Items.AddRange(new object[] { "None", "StartTls", "ImplicitTls" });
        cmbTls.SelectedItem = draft.Security.ToString();
        panel.Controls.Add(cmbTls);
        var chkAuth = new CheckBox { Text = "Autentizace", Checked = draft.UseAuthentication, AutoSize = true };
        panel.Controls.Add(chkAuth);
        var txtUser = Txt("Username:", draft.Username);
        var txtPass = Txt("Password:", draft.Password, password: true);
        var chkEnabled = new CheckBox { Text = "Enabled", Checked = draft.Enabled, AutoSize = true };
        panel.Controls.Add(chkEnabled);

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90 };
        var cancel = new Button { Text = "Zrušit", DialogResult = DialogResult.Cancel, Width = 90 };
        var btnRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        btnRow.Controls.Add(ok);
        btnRow.Controls.Add(cancel);
        panel.Controls.Add(btnRow);
        dlg.Controls.Add(panel);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog(this) != DialogResult.OK)
            return false;

        var security = Enum.TryParse<SmtpSecurity>(cmbTls.SelectedItem?.ToString(), true, out var sec)
            ? sec
            : SmtpSecurity.StartTls;
        draft = new SmtpAccountDraft(
            txtId.Text.Trim(),
            txtHost.Text.Trim(),
            (int)numPort.Value,
            security,
            chkAuth.Checked,
            txtUser.Text,
            txtPass.Text,
            chkEnabled.Checked);
        return true;
    }
}
