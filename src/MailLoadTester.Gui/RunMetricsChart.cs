namespace MailLoadTester.Gui;

/// <summary>
/// Bounded multi-series run chart. SMTP counters are cumulative attempt/logical
/// counts; IMAP quota-message delta is an independent observed mailbox change.
/// Missing IMAP measurements are gaps, never zero.
/// </summary>
internal sealed class RunMetricsChart : Control
{
    private const int MaxSamples = 120;

    private sealed record Sample(
        DateTimeOffset At,
        int SendAttempts,
        int Accepted,
        int Smtp4xx,
        int Smtp5xx,
        long? ObservedMailboxMessageDelta);

    private readonly List<Sample> _samples = new();

    public RunMetricsChart()
    {
        DoubleBuffered = true;
        MinimumSize = new Size(260, 170);
        BackColor = Color.FromArgb(250, 250, 250);
        ForeColor = Color.FromArgb(45, 55, 65);
        Font = new Font("Segoe UI", 8f);
        ResizeRedraw = true;
    }

    public void AddSample(
        DateTimeOffset at,
        int sendAttempts,
        int accepted,
        int smtp4xx,
        int smtp5xx,
        long? observedMailboxMessageDelta)
    {
        _samples.Add(new Sample(
            at,
            Math.Max(0, sendAttempts),
            Math.Max(0, accepted),
            Math.Max(0, smtp4xx),
            Math.Max(0, smtp5xx),
            observedMailboxMessageDelta));

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

        g.DrawString("SMTP a schránka – živé metriky", Font, textBrush, 6, 4);
        DrawLegend(g);

        var plot = new Rectangle(54, 42, Math.Max(1, Width - 68), Math.Max(1, Height - 76));
        g.DrawRectangle(border, plot);

        if (_samples.Count == 0)
        {
            g.DrawString("Čekám na průběh SMTP testu…", Font, mutedBrush,
                plot.Left + 10, plot.Top + plot.Height / 2 - 8);
            return;
        }

        var values = _samples.SelectMany(s => new long?[]
        {
            s.SendAttempts,
            s.Accepted,
            s.Smtp4xx,
            s.Smtp5xx,
            s.ObservedMailboxMessageDelta
        }).Where(v => v.HasValue).Select(v => v!.Value).ToArray();

        var min = Math.Min(0L, values.Min());
        var max = Math.Max(1L, values.Max());
        if (min == max) max = min + 1;
        var padding = Math.Max(1d, (max - min) * 0.08d);
        var yMin = min - padding;
        var yMax = max + padding;

        for (var tick = 0; tick <= 4; tick++)
        {
            var ratio = tick / 4d;
            var y = plot.Bottom - (float)(plot.Height * ratio);
            var value = yMin + (yMax - yMin) * ratio;
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
            var label = Math.Round(value).ToString("N0");
            var size = g.MeasureString(label, Font);
            g.DrawString(label, Font, mutedBrush, Math.Max(1, plot.Left - size.Width - 5), y - size.Height / 2);
        }

        DrawSeries(g, plot, yMin, yMax, Color.FromArgb(70, 90, 180), s => s.SendAttempts);
        DrawSeries(g, plot, yMin, yMax, Color.FromArgb(35, 145, 85), s => s.Accepted);
        DrawSeries(g, plot, yMin, yMax, Color.FromArgb(220, 145, 35), s => s.Smtp4xx);
        DrawSeries(g, plot, yMin, yMax, Color.FromArgb(190, 55, 55), s => s.Smtp5xx);
        DrawSeries(g, plot, yMin, yMax, Color.FromArgb(25, 135, 145), s => s.ObservedMailboxMessageDelta);

        var first = _samples[0].At.ToLocalTime().ToString("HH:mm:ss");
        var last = _samples[^1].At.ToLocalTime().ToString("HH:mm:ss");
        g.DrawString(first, Font, mutedBrush, plot.Left, plot.Bottom + 6);
        var lastSize = g.MeasureString(last, Font);
        g.DrawString(last, Font, mutedBrush, plot.Right - lastSize.Width, plot.Bottom + 6);
    }

    private void DrawLegend(Graphics g)
    {
        var items = new (string Label, Color Color)[]
        {
            ("SEND pokusy", Color.FromArgb(70, 90, 180)),
            ("SMTP Accepted", Color.FromArgb(35, 145, 85)),
            ("4xx", Color.FromArgb(220, 145, 35)),
            ("5xx", Color.FromArgb(190, 55, 55)),
            ("IMAP Δ (pozor.)", Color.FromArgb(25, 135, 145))
        };

        var x = 6f;
        const float y = 22f;
        using var textBrush = new SolidBrush(ForeColor);
        foreach (var item in items)
        {
            using var brush = new SolidBrush(item.Color);
            g.FillRectangle(brush, x, y + 2, 8, 8);
            x += 11;
            g.DrawString(item.Label, Font, textBrush, x, y - 1);
            x += g.MeasureString(item.Label, Font).Width + 8;
        }
    }

    private void DrawSeries(
        Graphics g,
        Rectangle plot,
        double yMin,
        double yMax,
        Color color,
        Func<Sample, long?> selector)
    {
        using var pen = new Pen(color, 1.8f);
        using var brush = new SolidBrush(color);
        PointF? previous = null;

        for (var index = 0; index < _samples.Count; index++)
        {
            var value = selector(_samples[index]);
            if (!value.HasValue)
            {
                previous = null;
                continue;
            }

            var x = plot.Left + plot.Width * index / (float)Math.Max(1, _samples.Count - 1);
            var ratio = (value.Value - yMin) / (yMax - yMin);
            var y = plot.Bottom - (float)(plot.Height * Math.Clamp(ratio, 0d, 1d));
            var current = new PointF(x, y);

            if (previous.HasValue)
                g.DrawLine(pen, previous.Value, current);

            g.FillEllipse(brush, current.X - 2.2f, current.Y - 2.2f, 4.4f, 4.4f);
            previous = current;
        }
    }
}
