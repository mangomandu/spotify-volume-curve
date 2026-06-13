using System.Drawing.Drawing2D;

namespace SpotifyLinearVolume;

/// <summary>Slim horizontal volume bar shown in the collapsed state. Drag to set position.</summary>
public sealed class VolumeBar : Control
{
    private static readonly Color Accent = Color.FromArgb(30, 215, 96);

    private int _pad = 14;
    private float _position;
    private bool _dragging;

    public event Action<float>? PositionPicked;

    /// <summary>Horizontal inset of the track from the control edges. The overlay sets this small
    /// (just enough for the knob) so the green track spans Spotify's rail edge-to-edge.</summary>
    public int EdgePad
    {
        get => _pad;
        set { _pad = Math.Max(0, value); Invalidate(); }
    }

    public VolumeBar()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(20, 20, 20);
        Cursor = Cursors.Hand;
        Height = 28;
    }

    public void Set(float position)
    {
        _position = Math.Clamp(position, 0f, 1f);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { Capture = true; _dragging = true; Pick(e.X); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) Pick(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _dragging = false; Capture = false; }
        base.OnMouseUp(e); // raises MouseUp so the overlay can show its right-click menu
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        _dragging = false; // capture lost (menu opened / hidden / focus change) → stop dragging
        base.OnMouseCaptureChanged(e);
    }

    private void Pick(int mouseX)
    {
        int width = Math.Max(1, Width - 2 * _pad);
        PositionPicked?.Invoke(Math.Clamp((float)(mouseX - _pad) / width, 0f, 1f));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int y = Height / 2;
        int x0 = _pad, x1 = Width - _pad;
        if (x1 <= x0) return;

        using (var track = new Pen(Color.FromArgb(60, 60, 60), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(track, x0, y, x1, y);

        int fx = x0 + (int)((x1 - x0) * _position);
        if (fx > x0)
            using (var fill = new Pen(Accent, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(fill, x0, y, fx, y);

        // Keep the 12px knob fully inside the control even when EdgePad is tiny (overlay mode),
        // so a thin overlay never has to extend past Spotify's rail to avoid clipping the knob.
        int kx = (x1 - x0 >= 12) ? Math.Clamp(fx, x0 + 6, x1 - 6) : (x0 + x1) / 2;
        using (var dot = new SolidBrush(Color.White))
            g.FillEllipse(dot, kx - 6, y - 6, 12, 12);
        using (var ring = new Pen(Accent, 2f))
            g.DrawEllipse(ring, kx - 6, y - 6, 12, 12);
    }
}
