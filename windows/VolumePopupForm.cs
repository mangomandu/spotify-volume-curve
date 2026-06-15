using System.Runtime.InteropServices;

namespace Volumify;

/// <summary>
/// A comfortable fly-out volume slider that pops up just above Spotify's playbar when you hover
/// the overlay. It exists because the in-place overlay shrinks to Spotify's (small) native rail —
/// fine to read, fiddly to drag when the window is narrow. The popup is always a generous width,
/// so you get precise control no matter how small the rail got. Non-activating, so it never steals
/// focus from Spotify, and it shows the perceptual % above the bar.
/// </summary>
public sealed class VolumePopupForm : Form
{
    private const int PopupWidth = 220;
    private const int PopupHeight = 52;

    private readonly VolumeModel _model;
    private readonly VolumeBar _bar = new();
    private readonly Label _readout = new();

    public VolumePopupForm(VolumeModel model)
    {
        _model = model;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(28, 28, 28);
        ClientSize = new Size(PopupWidth, PopupHeight);

        _readout.AutoSize = false;
        _readout.Dock = DockStyle.Top;
        _readout.Height = 18;
        _readout.TextAlign = ContentAlignment.MiddleCenter;
        _readout.ForeColor = Color.FromArgb(210, 210, 210);
        _readout.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        _readout.BackColor = Color.Transparent;
        Controls.Add(_readout);

        _bar.EdgePad = 16;          // roomy track + knob
        _bar.Dock = DockStyle.Fill;
        _bar.BackColor = BackColor; // blend the bar into the popup
        _bar.PositionPicked += pos => _model.SetPosition(pos);
        Controls.Add(_bar);
        _bar.BringToFront();

        Sync();
        _model.Changed += OnModelChanged;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
    }

    private void OnModelChanged()
    {
        if (!IsDisposed) Sync();
    }

    private void Sync()
    {
        _bar.Set(_model.Position);
        _readout.Text = $"{_model.Position * 100:0}%";
    }

    /// <summary>Position the popup centred over <paramref name="anchor"/> and sitting just above it.</summary>
    public void ShowAbove(Rectangle anchor)
    {
        int x = anchor.Left + anchor.Width / 2 - PopupWidth / 2;
        int y = anchor.Top - PopupHeight - 8;

        var wa = Screen.FromRectangle(anchor).WorkingArea;
        x = Math.Clamp(x, wa.Left + 4, wa.Right - PopupWidth - 4);
        if (y < wa.Top + 4) y = anchor.Bottom + 8; // no room above → drop below
        Location = new Point(x, y);

        if (!Visible) Show();
        // The overlay we hover sits just above Spotify; this popup is drawn over Spotify's window
        // area, so it must clear Spotify in the z-order too or it renders behind it (invisible).
        // Raise to the top without stealing focus.
        SetWindowPos(Handle, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    /// <summary>True while the cursor is over the popup (so the hover owner keeps it open).</summary>
    public bool ContainsCursor(int inflate)
    {
        if (!Visible) return false;
        var b = Bounds;
        b.Inflate(inflate, inflate);
        return b.Contains(Cursor.Position);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _model.Changed -= OnModelChanged;
        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
}
