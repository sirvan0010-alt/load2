namespace MailLoadTester.Gui;

/// <summary>
/// Bounded history of the effective global SEND interval and the live adaptive
/// concurrency ceiling. A missing concurrency value means adaptive control is off.
/// </summary>
internal sealed class AdaptiveControlChart : Control
{
    private readonly AdaptiveControlChartSampler _sampler = new();
    private IReadOnlyList<AdaptiveControlChartSample> Samples => _sampler.Samples;

    public AdaptiveControlChart()
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
        int? effectiveEffectiveIntervalMs,
        int? adaptiveConcurrency,
        int configuredConcurrency)
    {
        if (_sampler.AddSample(at, effectiveEffectiveIntervalMs, adaptiveConcurrency, configuredConcurrency))
            Invalidate();
    }

    public void ClearSamples()
    {
        _sampler.Clear();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        using var textBrush = new SolidBrush(ForeColor);
        using var mutedBrush = new SolidBrush(Color.DimGray);
        using var grid = new Pen(Color.FromArgb(225, 229, 233));
        using var border = new Pen(Color.FromArgb(210, 215, 220));

        g.DrawString("Adaptivní řízení – posledních 120 vzorků", Font, textBrush, 6, 4);

        var latest = Samples.LastOrDefault();
        var intervalText = latest?.EffectiveIntervalMs is int interval
            ? interval == 0
                ? "SEND interval: 0 ms (bez pevného intervalu)"
                : $"SEND interval: {interval:N0} ms (strop přibližně {60_000d / interval:F1} SEND/min)"
            : "SEND interval: —";
        var concurrencyText = latest?.AdaptiveConcurrency is int current
            ? $"Adaptive concurrency: {current}/{latest.ConfiguredConcurrency} workerů"
            : "Adaptive concurrency: vypnuto / bez vzorku";
        g.DrawString(intervalText, Font, mutedBrush, 6, 20);
        g.DrawString(concurrencyText, Font, mutedBrush, 6, 34);

        if (Samples.Count == 0)
        {
            g.DrawString("Čekám na průběh testu…", Font, mutedBrush, 12, 58);
            return;
        }

        var left = 58;
        var width = Math.Max(1, Width - left - 34);
        var firstTop = 62;
        var panelGap = 30;
        var panelHeight = Math.Max(28, (Height - firstTop - panelGap - 20) / 2);
        var intervalPlot = new Rectangle(left, firstTop, width, panelHeight);
        var concurrencyPlot = new Rectangle(left, firstTop + panelHeight + panelGap, width, panelHeight);

        var intervalMax = Math.Max(1_000, Samples
            .Where(sample => sample.EffectiveIntervalMs.HasValue)
            .Select(sample => sample.EffectiveIntervalMs!.Value)
            .DefaultIfEmpty(1_000)
            .Max());
        intervalMax = RoundAxisMaximum(intervalMax);

        var concurrencyMax = Math.Max(1, Samples.Max(sample => sample.ConfiguredConcurrency));

        DrawPanel(g, intervalPlot, "Globální SEND interval (ms)", 0, intervalMax,
            sample => sample.EffectiveIntervalMs, grid, border, Color.FromArgb(70, 90, 180), mutedBrush);

        if (Samples.Any(sample => sample.AdaptiveConcurrency.HasValue))
        {
            DrawPanel(g, concurrencyPlot, "Povolená adaptivní souběžnost (workery)", 0, concurrencyMax,
                sample => sample.AdaptiveConcurrency, grid, border, Color.FromArgb(25, 135, 145), mutedBrush);
        }
        else
        {
            DrawEmptyPanel(g, concurrencyPlot, "Povolená adaptivní souběžnost (workery)",
                "Adaptive concurrency je vypnuté.", grid, border, mutedBrush);
        }

        var first = Samples[0].At.ToLocalTime().ToString("HH:mm:ss");
        var last = Samples[^1].At.ToLocalTime().ToString("HH:mm:ss");
        g.DrawString(first, Font, mutedBrush, concurrencyPlot.Left, concurrencyPlot.Bottom + 3);
        var lastSize = g.MeasureString(last, Font);
        g.DrawString(last, Font, mutedBrush, concurrencyPlot.Right - lastSize.Width, concurrencyPlot.Bottom + 3);
    }

    private void DrawPanel(
        Graphics g,
        Rectangle plot,
        string title,
        int minimum,
        int maximum,
        Func<AdaptiveControlChartSample, int?> selector,
        Pen grid,
        Pen border,
        Color seriesColor,
        Brush mutedBrush)
    {
        using var textBrush = new SolidBrush(ForeColor);
        using var line = new Pen(seriesColor, 1.8f);
        using var pointBrush = new SolidBrush(seriesColor);

        g.DrawString(title, Font, textBrush, 6, plot.Top - 15);
        g.DrawRectangle(border, plot);

        for (var tick = 0; tick <= 2; tick++)
        {
            var ratio = tick / 2d;
            var y = plot.Bottom - (float)(plot.Height * ratio);
            var value = minimum + (maximum - minimum) * ratio;
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
            var label = Math.Round(value).ToString("N0");
            var size = g.MeasureString(label, Font);
            g.DrawString(label, Font, mutedBrush, Math.Max(1, plot.Left - size.Width - 5), y - size.Height / 2);
        }

        PointF? previous = null;
        for (var index = 0; index < Samples.Count; index++)
        {
            var value = selector(Samples[index]);
            if (!value.HasValue)
            {
                previous = null;
                continue;
            }

            var ratio = maximum > minimum
                ? (value.Value - minimum) / (double)(maximum - minimum)
                : 0d;
            var x = GetX(plot, index);
            var y = plot.Bottom - (float)(plot.Height * Math.Clamp(ratio, 0d, 1d));
            var current = new PointF(x, y);
            if (previous.HasValue)
                g.DrawLine(line, previous.Value, current);
            g.FillEllipse(pointBrush, current.X - 2.2f, current.Y - 2.2f, 4.4f, 4.4f);
            previous = current;
        }
    }

    private void DrawEmptyPanel(
        Graphics g,
        Rectangle plot,
        string title,
        string message,
        Pen grid,
        Pen border,
        Brush mutedBrush)
    {
        using var textBrush = new SolidBrush(ForeColor);
        g.DrawString(title, Font, textBrush, 6, plot.Top - 15);
        g.DrawRectangle(border, plot);
        for (var tick = 0; tick <= 2; tick++)
        {
            var y = plot.Bottom - plot.Height * tick / 2f;
            g.DrawLine(grid, plot.Left, y, plot.Right, y);
        }
        var size = g.MeasureString(message, Font);
        g.DrawString(message, Font, mutedBrush,
            plot.Left + Math.Max(4, (plot.Width - size.Width) / 2),
            plot.Top + Math.Max(2, (plot.Height - size.Height) / 2));
    }

    private float GetX(Rectangle plot, int index)
    {
        if (Samples.Count <= 1)
            return plot.Left;

        var first = Samples[0].At;
        var last = Samples[^1].At;
        var duration = (last - first).TotalMilliseconds;
        if (duration <= 0)
            return plot.Left + plot.Width * index / (float)(Samples.Count - 1);

        var ratio = (Samples[index].At - first).TotalMilliseconds / duration;
        return plot.Left + plot.Width * (float)Math.Clamp(ratio, 0d, 1d);
    }

    private static int RoundAxisMaximum(int value)
    {
        if (value <= 1_000) return 1_000;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        var rounded = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return (int)Math.Min(int.MaxValue, rounded * magnitude);
    }
}
