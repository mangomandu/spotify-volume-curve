using System.Drawing.Drawing2D;

namespace SpotifyLinearVolume;

/// <summary>
/// Live volume curve (gain = position ^ p): green line with a gradient fill, a linear
/// reference, and a glowing marker at the current position. Drag horizontally to set position.
/// </summary>
public sealed class CurveGraphPanel : Panel
{
    private const int Pad = 12;
    private static readonly Color Accent = Color.FromArgb(30, 215, 96);

    private float _p = 0.5f;
    private float _position = 0.5f;
    private bool _dragging;

    public event Action<float>? PositionPicked;

    public CurveGraphPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(26, 26, 26);
        Cursor = Cursors.Hand;
    }

    public void Set(float p, float position)
    {
        _p = p;
        _position = position;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e) { _dragging = true; Pick(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { if (_dragging) Pick(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { _dragging = false; }

    private void Pick(int mouseX)
    {
        int width = Math.Max(1, Width - 2 * Pad);
        PositionPicked?.Invoke(Math.Clamp((float)(mouseX - Pad) / width, 0f, 1f));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var area = new Rectangle(Pad, Pad, Width - 2 * Pad, Height - 2 * Pad);
        if (area.Width < 8 || area.Height < 8) return;

        using (var grid = new Pen(Color.FromArgb(40, 40, 40)))
            for (int i = 0; i <= 4; i++)
            {
                int x = area.Left + area.Width * i / 4;
                int y = area.Top + area.Height * i / 4;
                g.DrawLine(grid, x, area.Top, x, area.Bottom);
                g.DrawLine(grid, area.Left, y, area.Right, y);
            }

        using (var lin = new Pen(Color.FromArgb(70, 70, 70)) { DashStyle = DashStyle.Dash })
            g.DrawLine(lin, area.Left, area.Bottom, area.Right, area.Top);

        const int n = 128;
        var pts = new PointF[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float x = i / (float)n;
            float y = (float)Math.Pow(x, _p);
            pts[i] = new PointF(area.Left + x * area.Width, area.Bottom - y * area.Height);
        }

        using (var fillPath = new GraphicsPath())
        {
            fillPath.AddLines(pts);
            fillPath.AddLine(area.Right, area.Bottom, area.Left, area.Bottom);
            fillPath.CloseFigure();
            using var grad = new LinearGradientBrush(
                new Point(0, area.Top), new Point(0, area.Bottom + 1),
                Color.FromArgb(95, Accent), Color.FromArgb(8, Accent));
            g.FillPath(grad, fillPath);
        }

        using (var pen = new Pen(Accent, 2.6f) { LineJoin = LineJoin.Round })
            g.DrawLines(pen, pts);

        float gain = (float)Math.Pow(_position, _p);
        float mx = area.Left + _position * area.Width;
        float my = area.Bottom - gain * area.Height;
        using (var guide = new Pen(Color.FromArgb(85, 255, 255, 255)) { DashStyle = DashStyle.Dot })
        {
            g.DrawLine(guide, mx, area.Bottom, mx, my);
            g.DrawLine(guide, area.Left, my, mx, my);
        }
        using (var glow = new SolidBrush(Color.FromArgb(55, Accent)))
            g.FillEllipse(glow, mx - 9, my - 9, 18, 18);
        using (var dot = new SolidBrush(Color.White))
            g.FillEllipse(dot, mx - 4.5f, my - 4.5f, 9, 9);
        using (var ring = new Pen(Accent, 2f))
            g.DrawEllipse(ring, mx - 4.5f, my - 4.5f, 9, 9);
    }
}
