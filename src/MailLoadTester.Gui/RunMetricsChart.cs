namespace MailLoadTester.Gui;

/// <summary>
/// Bounded live rates for SMTP counters and independently observed mailbox deltas.
/// SMTP rates use a rolling baseline of up to ten seconds; IMAP message rate is
/// averaged between distinct quota snapshots. SMTP counter resets at an AutoRestart
/// boundary create a gap instead of a misleading negative rate.
/// </summary>
internal sealed class RunMetricsChart : Control
{
    private const int MaxSamples = 120;
    private static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(10);

    private sealed record RawSample(
        DateTimeOffset At,
        int SendAttempts,
        int Accepted,
        int Smtp4xx,
        int Smtp5xx,
        double? ObservedMailboxMessagesPerMinute,
        double? QuotaUsagePercent);

    private sealed record Sample(
        DateTimeOffset At,
        double? SendAttemptsPerMinute,
        double? AcceptedPerMinute,
        double? Smtp4xxPerMinute,
        double? Smtp5xxPerMinute,
        double? ObservedMailboxMessagesPerMinute,
        double? QuotaUsagePercent);

    private readonly List<RawSample> _rawSamples = new();
    private readonly List<Sample> _samples = new();
    private DateTimeOffset? _lastChartSampleAt;
    private DateTimeOffset? _lastQuotaObservationAt;
    private long? _lastQuotaMessageDelta;
    private double? _latestObservedMailboxMessagesPerMinute;
    private double? _latestQuotaUsagePercent;

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
        DateTimeOffset? quotaObservedAt,
        long? observedMailboxMessageDelta,
        double? quotaUsagePercent)
    {
        // IMAP observations arrive roughly every 30 seconds. Calculate their
        // rate only between distinct server snapshots, never from repeated
        // progress callbacks carrying the same cumulative delta.
        if (quotaObservedAt.HasValue
            && (!_lastQuotaObservationAt.HasValue || quotaObservedAt.Value > _lastQuotaObservationAt.Value))
        {
            if (_lastQuotaObservationAt.HasValue
                && _lastQuotaMessageDelta.HasValue
                && observedMailboxMessageDelta.HasValue)
            {
                var elapsed = (quotaObservedAt.Value - _lastQuotaObservationAt.Value).TotalMinutes;
                _latestObservedMailboxMessagesPerMinute = elapsed > 0
                    ? (observedMailboxMessageDelta.Value - _lastQuotaMessageDelta.Value) / elapsed
                    : null;
            }
            else
            {
                _latestObservedMailboxMessagesPerMinute = null;
            }

            _lastQuotaObservationAt = quotaObservedAt.Value;
            _lastQuotaMessageDelta = observedMailboxMessageDelta;
            _latestQuotaUsagePercent = quotaUsagePercent is >= 0 and <= 100 ? quotaUsagePercent : null;
        }

        // Progress events can arrive for every message/state transition. Keep
        // a one-second chart cadence so 120 samples represent about two minutes.
        if (_lastChartSampleAt.HasValue
            && at - _lastChartSampleAt.Value < TimeSpan.FromSeconds(1))
        {
            Invalidate();
            return;
        }

        _lastChartSampleAt = at;
        var raw = new RawSample(
            at,
            Math.Max(0, sendAttempts),
            Math.Max(0, accepted),
            Math.Max(0, smtp4xx),
            Math.Max(0, smtp5xx),
            _latestObservedMailboxMessagesPerMinute,
            _latestQuotaUsagePercent);

        _rawSamples.Add(raw);
        if (_rawSamples.Count > MaxSamples)
            _rawSamples.RemoveRange(0, _rawSamples.Count - MaxSamples);

        var cutoff = at - RateWindow;
        var baseline = _rawSamples.LastOrDefault(sample => sample.At <= cutoff)
            ?? _rawSamples[0];
        var elapsedMinutes = (at - baseline.At).TotalMinutes;

        double? NonNegativeRate(int current, int previous) =>
            elapsedMinutes > 0 && current >= previous
                ? (current - previous) / elapsedMinutes
                : null;

        _samples.Add(new Sample(
            at,
            NonNegativeRate(raw.SendAttempts, baseline.SendAttempts),
            NonNegativeRate(raw.Accepted, baseline.Accepted),
            NonNegativeRate(raw.Smtp4xx, baseline.Smtp4xx),
            NonNegativeRate(raw.Smtp5xx, baseline.Smtp5xx),
            raw.ObservedMailboxMessagesPerMinute,
            raw.QuotaUsagePercent));

        if (_samples.Count > MaxSamples)
            _samples.RemoveRange(0, _samples.Count - MaxSamples);

        Invalidate();
    }

    public void ClearSamples()
    {
        _rawSamples.Clear();
        _samples.Clear();
        _lastChartSampleAt = null;
        _lastQuotaObservationAt = null;
        _lastQuotaMessageDelta = null;
        _latestObservedMailboxMessagesPerMinute = null;
        _latestQuotaUsagePercent = null;
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

        g.DrawString("SMTP a schránka – rychlost v čase", Font, textBrush, 6, 4);
        DrawLegend(g);

        var plot = new Rectangle(54, 52, Math.Max(1, Width - 82), Math.Max(1, Height - 86));
        g.DrawRectangle(border, plot);

        if (_samples.Count == 0)
        {
            g.DrawString("Čekám na průběh SMTP testu…", Font, mutedBrush,
                plot.Left + 10, plot.Top + plot.Height / 2 - 8);
            return;
        }

        var values = _samples.SelectMany(sample => new double?[]
        {
            sample.SendAttemptsPerMinute,
            sample.AcceptedPerMinute,
            sample.Smtp4xxPerMinute,
            sample.Smtp5xxPerMinute,
            sample.ObservedMailboxMessagesPerMinute
        }).Where(value => value.HasValue).Select(value => value!.Value).ToArray();

        if (values.Length == 0)
        {
            g.DrawString("Počítám rychlost z časového okna…", Font, mutedBrush,
                plot.Left + 10, plot.Top + plot.Height / 2 - 8);
        }
        else
        {
            var min = Math.Min(0d, values.Min());
            var max = Math.Max(1d, values.Max());
            var padding = Math.Max(0.5d, (max - min) * 0.08d);
            var yMin = min - padding;
            var yMax = max + padding;

            for (var tick = 0; tick <= 4; tick++)
            {
                var ratio = tick / 4d;
                var y = plot.Bottom - (float)(plot.Height * ratio);
                var value = yMin + (yMax - yMin) * ratio;
                g.DrawLine(grid, plot.Left, y, plot.Right, y);
                var label = Math.Abs(value) < 10 ? value.ToString("F1") : Math.Round(value).ToString("N0");
                var size = g.MeasureString(label, Font);
                g.DrawString(label, Font, mutedBrush, Math.Max(1, plot.Left - size.Width - 5), y - size.Height / 2);
            }

            DrawSeries(g, plot, yMin, yMax, Color.FromArgb(70, 90, 180), sample => sample.SendAttemptsPerMinute);
            DrawSeries(g, plot, yMin, yMax, Color.FromArgb(35, 145, 85), sample => sample.AcceptedPerMinute);
            DrawSeries(g, plot, yMin, yMax, Color.FromArgb(220, 145, 35), sample => sample.Smtp4xxPerMinute);
            DrawSeries(g, plot, yMin, yMax, Color.FromArgb(190, 55, 55), sample => sample.Smtp5xxPerMinute);
            DrawSeries(g, plot, yMin, yMax, Color.FromArgb(25, 135, 145), sample => sample.ObservedMailboxMessagesPerMinute);
        }

        DrawQuotaPercentSeries(g, plot, Color.MediumPurple, sample => sample.QuotaUsagePercent);
        for (var tick = 0; tick <= 4; tick++)
        {
            var y = plot.Bottom - plot.Height * tick / 4f;
            g.DrawString($"{tick * 25}%", Font, mutedBrush, plot.Right + 3, y - 6);
        }

        var first = _samples[0].At.ToLocalTime().ToString("HH:mm:ss");
        var last = _samples[^1].At.ToLocalTime().ToString("HH:mm:ss");
        g.DrawString(first, Font, mutedBrush, plot.Left, plot.Bottom + 6);
        var lastSize = g.MeasureString(last, Font);
        g.DrawString(last, Font, mutedBrush, plot.Right - lastSize.Width, plot.Bottom + 6);
        g.DrawString("levá osa: zprávy/min; pravá osa: kvóta %; IMAP Δ/min = průměr mezi snapshoty", Font, mutedBrush, 6, Height - 15);
    }

    private void DrawLegend(Graphics g)
    {
        var items = new (string Label, Color Color)[]
        {
            ("SEND/min", Color.FromArgb(70, 90, 180)),
            ("Accepted/min", Color.FromArgb(35, 145, 85)),
            ("4xx/min", Color.FromArgb(220, 145, 35)),
            ("5xx/min", Color.FromArgb(190, 55, 55)),
            ("IMAP Δ/min*", Color.FromArgb(25, 135, 145)),
            ("Kvóta %", Color.MediumPurple)
        };

        using var textBrush = new SolidBrush(ForeColor);
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var column = index % 3;
            var row = index / 3;
            var x = 6f + column * Math.Max(82, (Width - 12) / 3f);
            var y = 20f + row * 13f;
            using var brush = new SolidBrush(item.Color);
            g.FillRectangle(brush, x, y + 2, 8, 8);
            g.DrawString(item.Label, Font, textBrush, x + 11, y - 1);
        }
    }

    private void DrawQuotaPercentSeries(
        Graphics g,
        Rectangle plot,
        Color color,
        Func<Sample, double?> selector)
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

            var x = GetX(plot, index);
            var y = plot.Bottom - (float)(plot.Height * Math.Clamp(value.Value / 100d, 0d, 1d));
            var current = new PointF(x, y);
            if (previous.HasValue)
                g.DrawLine(pen, previous.Value, current);
            g.FillEllipse(brush, current.X - 2.2f, current.Y - 2.2f, 4.4f, 4.4f);
            previous = current;
        }
    }

    private void DrawSeries(
        Graphics g,
        Rectangle plot,
        double yMin,
        double yMax,
        Color color,
        Func<Sample, double?> selector)
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

            var ratio = (value.Value - yMin) / (yMax - yMin);
            var x = GetX(plot, index);
            var y = plot.Bottom - (float)(plot.Height * Math.Clamp(ratio, 0d, 1d));
            var current = new PointF(x, y);
            if (previous.HasValue)
                g.DrawLine(pen, previous.Value, current);
            g.FillEllipse(brush, current.X - 2.2f, current.Y - 2.2f, 4.4f, 4.4f);
            previous = current;
        }
    }

    private float GetX(Rectangle plot, int index)
    {
        if (_samples.Count <= 1)
            return plot.Left;

        var first = _samples[0].At;
        var last = _samples[^1].At;
        var duration = (last - first).TotalMilliseconds;
        if (duration <= 0)
            return plot.Left + plot.Width * index / (float)(_samples.Count - 1);

        var ratio = (_samples[index].At - first).TotalMilliseconds / duration;
        return plot.Left + plot.Width * (float)Math.Clamp(ratio, 0d, 1d);
    }
}
