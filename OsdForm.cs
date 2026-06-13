using System.Drawing.Drawing2D;

namespace SpotifyLinearVolume;

/// <summary>
/// Small on-screen volume overlay (OSD) shown near the bottom of the primary screen
/// whenever the volume changes. Non-activating so it never steals focus.
/// </summary>
public sealed class OsdForm : Form
{
    private readonly System.Windows.Forms.Timer _hideTimer;
    private float _fill;
    private string _text = "";

    public OsdForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(320, 92);
        BackColor = Color.FromArgb(18, 18, 18);
        Opacity = 0.88;
        DoubleBuffered = true;

        _hideTimer = new System.Windows.Forms.Timer { Interval = 1400 };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); Hide(); };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Stop();
            _hideTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void Flash(string text, float fill)
    {
        _text = text;
        _fill = Math.Clamp(fill, 0f, 1f);

        var wa = (Screen.PrimaryScreen ?? Screen.FromControl(this)).WorkingArea;
        Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - 80);
        Invalidate();

        if (!Visible) Show();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
        g.DrawString(_text, titleFont, Brushes.White, 18, 14);

        var track = new Rectangle(18, 54, Width - 36, 16);
        using (var back = new SolidBrush(Color.FromArgb(70, 70, 70)))
            g.FillRectangle(back, track);

        var fillRect = new Rectangle(track.X, track.Y, (int)(track.Width * _fill), track.Height);
        using (var fill = new SolidBrush(Color.FromArgb(168, 85, 247)))
            g.FillRectangle(fill, fillRect);
    }
}
