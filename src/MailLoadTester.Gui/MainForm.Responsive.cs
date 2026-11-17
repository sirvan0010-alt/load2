namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private bool _responsiveLayoutRunning;

    /// <summary>
    /// Reflows the SMTP page from three columns to one column on narrower windows
    /// and sizes controls to the actual width of their FlowLayoutPanel.
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        if (_responsiveLayoutRunning || IsDisposed || _smtpLayout is null || _smtpLayout.IsDisposed)
            return;

        _responsiveLayoutRunning = true;
        try
        {
            var compact = _smtpLayout.ClientSize.Width < 1120;
            if (_smtpLayout.ColumnCount != (compact ? 1 : 3))
            {
                _smtpLayout.SuspendLayout();
                _smtpLayout.ColumnStyles.Clear();
                _smtpLayout.RowStyles.Clear();
                _smtpLayout.ColumnCount = compact ? 1 : 3;
                _smtpLayout.RowCount = compact ? 3 : 1;

                if (compact)
                {
                    _smtpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    _smtpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 470F));
                    _smtpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 620F));
                    _smtpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
                    _smtpLayout.Dock = DockStyle.Top;
                    _smtpLayout.Height = 1340;
                    if (_smtpLayout.Parent is ScrollableControl compactParent) compactParent.AutoScroll = true;
                    for (var i = 0; i < _smtpLayout.Controls.Count; i++)
                        _smtpLayout.SetCellPosition(_smtpLayout.Controls[i], new TableLayoutPanelCellPosition(0, i));
                }
                else
                {
                    _smtpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
                    _smtpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
                    _smtpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
                    _smtpLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                    _smtpLayout.Dock = DockStyle.Fill;
                    _smtpLayout.Height = 0;
                    if (_smtpLayout.Parent is ScrollableControl wideParent) wideParent.AutoScroll = false;
                    for (var i = 0; i < _smtpLayout.Controls.Count; i++)
                        _smtpLayout.SetCellPosition(_smtpLayout.Controls[i], new TableLayoutPanelCellPosition(i, 0));
                }
                _smtpLayout.ResumeLayout(true);
            }

            ResizeFlowContent(this);
        }
        finally
        {
            _responsiveLayoutRunning = false;
        }
    }

    private static void ResizeFlowContent(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is FlowLayoutPanel flow &&
                flow.FlowDirection is FlowDirection.TopDown or FlowDirection.BottomUp)
            {
                var available = Math.Max(120, flow.ClientSize.Width - flow.Padding.Horizontal - 12);
                flow.SuspendLayout();
                foreach (Control item in flow.Controls)
                {
                    item.Margin = new Padding(item.Margin.Left, item.Margin.Top, 4, item.Margin.Bottom);
                    if (item is Label label)
                    {
                        if (label.AutoSize)
                            label.MaximumSize = new Size(available, 0);
                        else
                            label.Width = available;
                    }
                    else if (item is TextBox or ComboBox)
                    {
                        item.Width = available;
                    }
                    else if (item is CheckBox checkBox)
                    {
                        checkBox.AutoSize = false;
                        checkBox.Width = available;
                        var textSize = TextRenderer.MeasureText(checkBox.Text, checkBox.Font,
                            new Size(Math.Max(80, available - 28), 0), TextFormatFlags.WordBreak);
                        checkBox.Height = Math.Max(checkBox.Font.Height + 8, textSize.Height + 8);
                    }
                    else if (item is FlowLayoutPanel nested)
                    {
                        nested.Width = available;
                        if (nested.FlowDirection is FlowDirection.LeftToRight or FlowDirection.RightToLeft)
                            nested.WrapContents = true;
                    }
                    else if (item is Button)
                    {
                        item.Width = Math.Min(Math.Max(item.Width, 180), available);
                    }
                    else if (item is Panel or GroupBox or TableLayoutPanel)
                    {
                        item.Width = available;
                    }
                }
                flow.ResumeLayout(true);
            }

            if (child.HasChildren)
                ResizeFlowContent(child);
        }
    }
}
