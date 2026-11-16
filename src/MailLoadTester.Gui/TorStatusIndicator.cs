using System.Drawing.Drawing2D;

namespace MailLoadTester.Gui;

public enum TorIndicatorState
{
    Off,
    Checking,
    Verified,
    Failed
}

/// <summary>
/// Compact Tor onion status glyph for the main status strip.
/// A purple glow is shown only after the Tor Project check API confirms IsTor=true.
/// </summary>
public sealed class TorStatusIndicator : Control
{
    private TorIndicatorState _state;
    private string? _exitIp;

    public TorIndicatorState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            AccessibleDescription = value switch
            {
                TorIndicatorState.Verified => $"Tor ověřen. Výstupní IP: {_exitIp ?? "neznámá"}.",
                TorIndicatorState.Checking => "Ověřuji připojení k síti Tor.",
                TorIndicatorState.Failed => "Tor není ověřen. Přímý fallback není povolen.",
                _ => "Tor není aktivní."
            };
            Invalidate();
        }
    }

    public string? ExitIp
    {
        get => _exitIp;
        set
        {
            _exitIp = value;
            Invalidate();
        }
    }

    public TorStatusIndicator()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(84, 24);
        MinimumSize = new Size(84, 24);
        MaximumSize = new Size(84, 24);
        Margin = new Padding(2, 0, 6, 0);
        TabStop = false;
        AccessibleName = "Indikátor Tor";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var active = _state == TorIndicatorState.Verified;
        var checking = _state == TorIndicatorState.Checking;
        var failed = _state == TorIndicatorState.Failed;
        var purple = Color.FromArgb(145, 70, 210);

        if (active)
        {
            using var glow = new SolidBrush(Color.FromArgb(75, purple));
            g.FillEllipse(glow, 0, 0, 26, 24);
            using var innerGlow = new SolidBrush(Color.FromArgb(55, 190, 130, 255));
            g.FillEllipse(innerGlow, 3, 2, 20, 20);
        }

        var bulbColor = active ? Color.FromArgb(175, 105, 235)
            : checking ? Color.FromArgb(220, 165, 70)
            : failed ? Color.FromArgb(205, 75, 75)
            : SystemColors.GrayText;
        using var bulb = new GraphicsPath();
        bulb.AddBezier(12, 3, 5, 3, 3, 11, 7, 16);
        bulb.AddBezier(7, 16, 8, 19, 10, 20, 12, 20);
        bulb.AddBezier(12, 20, 14, 20, 16, 19, 17, 16);
        bulb.AddBezier(17, 16, 21, 11, 19, 3, 12, 3);
        using (var fill = new LinearGradientBrush(new Rectangle(4, 3, 17, 18),
                   ControlPaint.Light(bulbColor), ControlPaint.Dark(bulbColor), 90f))
            g.FillPath(fill, bulb);
        using (var outline = new Pen(active ? Color.FromArgb(225, 195, 255) : ControlPaint.Dark(bulbColor), 1.2f))
            g.DrawPath(outline, bulb);

        using (var band = new Pen(Color.FromArgb(active ? 210 : 150, Color.White), 1f))
        {
            g.DrawArc(band, 7, 7, 10, 7, 15, 150);
            g.DrawArc(band, 7, 10, 10, 7, 15, 150);
        }
        using (var stem = new Pen(bulbColor, 1.6f))
        {
            g.DrawLine(stem, 12, 1, 12, 4);
            g.DrawLine(stem, 9, 19, 15, 19);
        }

        var label = active ? "TOR ON" : checking ? "TOR…" : failed ? "TOR OFF" : "TOR";
        var textColor = active ? purple : failed ? Color.Firebrick : checking ? Color.DarkGoldenrod : SystemColors.GrayText;
        TextRenderer.DrawText(g, label, Font, new Point(27, 5), textColor,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
    }
}
