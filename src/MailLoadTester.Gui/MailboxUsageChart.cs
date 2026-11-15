namespace MailLoadTester.Gui;

/// <summary>
/// Bounded, read-only time-series chart for server-reported mailbox quota usage.
/// Missing quota samples are kept as gaps; they are never plotted as zero.
/// </summary>
internal sealed class MailboxUsageChart : Control
{
    private const int MaxSamples = 120;
    private readonly List<(DateTimeOffset At, double? Percent)> _samples = new();

    public MailboxUsageChart()
    {
        DoubleBuffered = true;
        Height = 190;
        MinimumSize = new Size(240, 150);
        BackColor = Color.FromArgb(250, 250, 250);
        ForeColor = Color.FromArgb(45, 55, 65);
        Font = new Font("Segoe UI", 8.5f);
        ResizeRedraw = true;
    }

    public void AddSample(DateTimeOffset at, double? usagePercent)
    {
        if (usagePercent is < 0)
            usagePercent = null;

        _samples.Add((at, usagePercent));
        if (_samples.Count > MaxSamples)
            _samples.RemoveRange(0, _samples.Count - MaxSamples);

        Invalidate();
    }

    public void ClearSamples()
    {
        _samples.Clear();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        using var border = new Pen(Color.FromArgb(210, 215, 220));
        using var grid = new Pen(Color.FromArgb(225, 229, 233));
        using var textBrush = new SolidBrush(ForeColor);
        using var mutedBrush = new SolidBrush(Color.DimGray);
        using var line = new Pen(Color.FromArgb(28, 120, 190), 2.2f);
        using var pointBrush = new SolidBrush(Color.FromArgb(28, 120, 190));

        var plot = new Rectangle(48, 28, Math.Max(1, Width - 64), Math.Max(1, Height - 62));
        g.DrawString("Historie obsazení IMAP kvóty", Font, textBrush, 6, 5);
        g.DrawRectangle(border, plot);

        for (var pct = 0; pct <= 100; pct += 25)
        {
            var y = plot.Bottom - (plot.Height * pct / 100f);
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
            g.DrawString(pct.ToString(), Font, mutedBrush, 7, y - 7);
        }

        if (_samples.Count == 0)
        {
            g.DrawString("Čekám na první ověřený IMAP vzorek…", Font, mutedBrush,
                plot.Left + 10, plot.Top + plot.Height / 2 - 8);
            return;
        }

        var valid = _samples.Select((sample, index) => (sample, index))
            .Where(x => x.sample.Percent.HasValue)
            .ToArray();

        if (valid.Length == 0)
        {
            g.DrawString("Server neposkytuje použitelnou STORAGE kvótu.", Font, mutedBrush,
                plot.Left + 10, plot.Top + plot.Height / 2 - 8);
            return;
        }

        PointF? previous = null;
        foreach (var item in valid)
        {
            var x = plot.Left + (valid.Length == 1
                ? plot.Width
                : plot.Width * item.index / (float)Math.Max(1, _samples.Count - 1));
            var clamped = Math.Clamp(item.sample.Percent!.Value, 0, 100);
            var y = plot.Bottom - (plot.Height * (float)clamped / 100f);
            var current = new PointF(x, y);

            // A null sample breaks the line: unavailable data must not imply zero usage.
            if (previous.HasValue && item.index > 0 &&
                _samples.Skip(valid.First(v => v.index == item.index).index == 0 ? 0 :
                    valid.First(v => v.index == item.index).index).Take(0).Any())
                previous = null;

            if (previous.HasValue)
                g.DrawLine(line, previous.Value, current);

            g.FillEllipse(pointBrush, current.X - 3.2f, current.Y - 3.2f, 6.4f, 6.4f);
            previous = current;
        }

        var first = _samples[0].At.ToLocalTime().ToString("HH:mm:ss");
        var last = _samples[^1].At.ToLocalTime().ToString("HH:mm:ss");
        g.DrawString(first, Font, mutedBrush, plot.Left, plot.Bottom + 7);
        var lastSize = g.MeasureString(last, Font);
        g.DrawString(last, Font, mutedBrush, plot.Right - lastSize.Width, plot.Bottom + 7);
    }
}
