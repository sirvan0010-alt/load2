namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private TextBox AddLabeledText(Control parent, string label, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var txt = new TextBox { Width = 360, Margin = new Padding(0, 0, 0, 4) };
        parent.Controls.Add(txt);
        toolTip.SetToolTip(txt, tip);
        return txt;
    }

    private NumericUpDown AddLabeledNumeric(Control parent, string label, decimal min, decimal max, decimal value, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var num = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 120, Margin = new Padding(0, 0, 0, 4) };
        parent.Controls.Add(num);
        toolTip.SetToolTip(num, tip);
        return num;
    }

    private ComboBox AddLabeledCombo(Control parent, string label, string[] items, string selected, string tip)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 8, 0, 2) };
        parent.Controls.Add(lbl);
        var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Margin = new Padding(0, 0, 0, 4) };
        cmb.Items.AddRange(items);
        cmb.SelectedItem = selected;
        parent.Controls.Add(cmb);
        toolTip.SetToolTip(cmb, tip);
        return cmb;
    }

    private CheckBox AddCheckBox(Control parent, string text, bool check, string tip)
    {
        var chk = new CheckBox { Text = text, AutoSize = true, Checked = check, Margin = new Padding(0, 6, 0, 2) };
        parent.Controls.Add(chk);
        toolTip.SetToolTip(chk, tip);
        return chk;
    }
}
