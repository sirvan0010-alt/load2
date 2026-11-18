namespace MailLoadTester.Gui;

public sealed partial class MainForm
{
    private bool _responsiveLayoutRunning;

    /// <summary>
    /// Reflows the SMTP page when three columns would make fields too narrow,
    /// then sizes controls in every vertical FlowLayoutPanel to the available width.
    /// This method deliberately does not change authentication behavior or controls.
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        if (_responsiveLayoutRunning || IsDisposed || _smtpLayout is null || _smtpLayout.IsDisposed)
            return;

        _responsiveLayoutRunning = true;
        try
        {
            // Three columns need enough room for usable input fields. The breakpoint
            // is based on the layout's client width, not the outer window dimensions.
            var compact = _smtpLayout.ClientSize.Width < 1120;
            var targetColumns = compact ? 1 : 3;

            if (_smtpLayout.ColumnCount != targetColumns)
            {
                _smtpLayout.SuspendLayout();
                try
                {
                    _smtpLayout.ColumnStyles.Clear();
                    _smtpLayout.RowStyles.Clear();
                    _smtpLayout.ColumnCount = targetColumns;
                    _smtpLayout.RowCount = compact ? 3 : 1;

                    if (compact)
                    {
                        _smtpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                        // Keep the sections independently scrollable rather than
                        // forcing all controls into one very tall, clipped row.
                        _smtpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 470F));
                        _smtpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 620F));
                        _smtpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
                        _smtpLayout.Dock = DockStyle.Top;
                        _smtpLayout.Height = 1340;
                        if (_smtpLayout.Parent is ScrollableControl compactParent)
                            compactParent.AutoScroll = true;

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
                        if (_smtpLayout.Parent is ScrollableControl wideParent)
                            wideParent.AutoScroll = false;

                        for (var i = 0; i < _smtpLayout.Controls.Count; i++)
                            _smtpLayout.SetCellPosition(_smtpLayout.Controls[i], new TableLayoutPanelCellPosition(i, 0));
                    }
                }
                finally
                {
                    _smtpLayout.ResumeLayout(true);
                }
            }

            // Let docking and table layout settle before measuring child widths.
            PerformLayout();
            ResizeFlowContent(this);
        }
        finally
        {
            _responsiveLayoutRunning = false;
        }
    }

    /// <summary>
    /// Makes the controls inside vertical flow panels follow their actual parent
    /// width. Labels wrap instead of retaining the old hard-coded 340/420/520 px
    /// maximum widths; editable fields no longer remain at a fixed 360 px width.
    /// </summary>
    private static void ResizeFlowContent(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is FlowLayoutPanel flow &&
                flow.Visible &&
                flow.ClientSize.Width > 0 &&
                flow.FlowDirection is FlowDirection.TopDown or FlowDirection.BottomUp)
            {
                var scrollbarAllowance = flow.AutoScroll ? SystemInformation.VerticalScrollBarWidth : 0;
                var available = Math.Max(140,
                    flow.ClientSize.Width - flow.Padding.Horizontal - scrollbarAllowance - 12);

                flow.SuspendLayout();
                try
                {
                    foreach (Control item in flow.Controls)
                    {
                        item.Margin = new Padding(item.Margin.Left, item.Margin.Top, 4, item.Margin.Bottom);

                        switch (item)
                        {
                            case Label label:
                                if (label.AutoSize)
                                {
                                    label.MaximumSize = new Size(available, 0);
                                }
                                else
                                {
                                    label.Width = available;
                                    var labelTextSize = TextRenderer.MeasureText(
                                        label.Text,
                                        label.Font,
                                        new Size(Math.Max(80, available - label.Padding.Horizontal - 8), int.MaxValue),
                                        TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                                    label.Height = Math.Max(
                                        label.Height,
                                        labelTextSize.Height + label.Padding.Vertical + 4);
                                }
                                break;

                            case TextBox:
                            case ComboBox:
                            case ListBox:
                            case DataGridView:
                                item.Width = available;
                                break;

                            case CheckBox checkBox:
                                checkBox.AutoSize = false;
                                checkBox.Width = available;
                                var textSize = TextRenderer.MeasureText(
                                    checkBox.Text,
                                    checkBox.Font,
                                    new Size(Math.Max(80, available - 28), 0),
                                    TextFormatFlags.WordBreak);
                                checkBox.Height = Math.Max(checkBox.Font.Height + 8, textSize.Height + 8);
                                break;

                            case FlowLayoutPanel nested:
                                nested.Width = available;
                                if (nested.FlowDirection is FlowDirection.LeftToRight or FlowDirection.RightToLeft)
                                    nested.WrapContents = true;
                                break;

                            case Button button:
                                button.Width = Math.Min(Math.Max(button.Width, 180), available);
                                break;

                            // TableLayoutPanel derives from Panel, so it must
                            // be matched before the general Panel case.
                            case TableLayoutPanel:
                            case GroupBox:
                                item.Width = available;
                                break;

                            case Panel panel:
                                panel.Width = available;
                                // The EML template picker is a manually positioned
                                // TextBox + browse Button inside a plain Panel.
                                // Resize both children together to avoid overflow.
                                var panelTextBox = panel.Controls.OfType<TextBox>().FirstOrDefault();
                                var panelButton = panel.Controls.OfType<Button>().FirstOrDefault();
                                if (panelTextBox is not null && panelButton is not null)
                                {
                                    panelButton.Left = Math.Max(0, available - panelButton.Width);
                                    panelTextBox.Width = Math.Max(80, panelButton.Left - panelTextBox.Left - 4);
                                }
                                break;
                        }
                    }
                }
                finally
                {
                    flow.ResumeLayout(true);
                }
            }

            if (child.HasChildren)
                ResizeFlowContent(child);
        }
    }
}
