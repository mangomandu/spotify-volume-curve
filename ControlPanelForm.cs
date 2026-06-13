using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace SpotifyLinearVolume;

/// <summary>
/// Borderless rounded control window. Expanded: curve graph + %/dB readout + preset pills.
/// Collapsed: a slim volume bar. Optionally docks just above the Spotify window and follows
/// it smoothly via a window-move event hook (no polling, so it keeps up with any refresh rate).
/// </summary>
public sealed class ControlPanelForm : Form
{
    private static readonly Color Bg = Color.FromArgb(20, 20, 20);
    private static readonly Color Accent = Color.FromArgb(30, 215, 96);
    private const int HeaderH = 42;
    private const int CollapsedH = 34;
    private const int CollapsedW = 184;

    private readonly VolumeModel _model;
    private readonly (string Label, float P)[] _presets;

    private readonly CurveGraphPanel _graph = new();
    private readonly PresetBar _presetBar;
    private readonly VolumeBar _bar = new();
    private readonly Label _close = new();
    private readonly Label _collapseBtn = new();

    private readonly System.Windows.Forms.Timer _presenceTimer = new() { Interval = 250 };
    private readonly WinEventProc _winEventProc;
    private IntPtr _winEventHook;
    private uint _hookedPid;

    private bool _dockMode, _userHidden, _userDragging, _hasAnchor, _collapsed;
    private IntPtr _spotifyHwnd;
    private uint _spotifyPid;
    private Point? _dockOffset;
    private Point _lastAnchor;
    private Size _expandedSize = new(300, 320);

    public event Action<Point>? DockOffsetChanged;
    public event Action<Size>? PanelBoundsChanged;
    public event Action<bool>? CollapsedChanged;

    public ControlPanelForm(VolumeModel model, (string Label, float P)[] presets)
    {
        _model = model;
        _presets = presets;
        _presetBar = new PresetBar(Array.ConvertAll(presets, p => ShortLabel(p.Label)));
        _winEventProc = OnWinEvent;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(300, 320);
        MinimumSize = new Size(260, 280);
        BackColor = Bg;
        ForeColor = Color.White;
        ShowInTaskbar = false;

        SetupHeaderButton(_collapseBtn, "▾", ClientSize.Width - 60);
        _collapseBtn.Click += (_, _) => ToggleCollapsed();

        SetupHeaderButton(_close, "✕", ClientSize.Width - 34);
        _close.Click += (_, _) => { _userHidden = true; Hide(); };

        _graph.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _graph.SetBounds(14, HeaderH, ClientSize.Width - 28, ClientSize.Height - HeaderH - 92);
        _graph.PositionPicked += pos => _model.SetPosition(pos);

        _presetBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _presetBar.SetBounds(14, ClientSize.Height - 38, ClientSize.Width - 28, 30);
        _presetBar.PresetSelected += i => _model.SetP(_presets[i].P);

        _bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _bar.SetBounds(16, 50, ClientSize.Width - 32, 28);
        _bar.Visible = false;
        _bar.PositionPicked += pos => _model.SetPosition(pos);

        Controls.Add(_collapseBtn);
        Controls.Add(_close);
        Controls.Add(_graph);
        Controls.Add(_presetBar);
        Controls.Add(_bar);

        _presenceTimer.Tick += (_, _) => PresenceTick();

        _model.Changed += OnModelChanged;
        OnModelChanged();
    }

    private static void SetupHeaderButton(Label l, string text, int x)
    {
        l.Text = text;
        l.Font = new Font("Segoe UI", 10f);
        l.ForeColor = Color.FromArgb(150, 150, 150);
        l.TextAlign = ContentAlignment.MiddleCenter;
        l.Size = new Size(26, 24);
        l.Cursor = Cursors.Hand;
        l.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        l.Location = new Point(x, 9);
        l.MouseEnter += (_, _) => l.ForeColor = Color.White;
        l.MouseLeave += (_, _) => l.ForeColor = Color.FromArgb(150, 150, 150);
    }

    private static string ShortLabel(string full)
    {
        int o = full.LastIndexOf('(');
        int c = full.LastIndexOf(')');
        return o >= 0 && c > o ? full.Substring(o + 1, c - o - 1) : full;
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); } catch { /* Win10: no-op */ }
    }

    // ----- collapse / expand -----
    private void ToggleCollapsed()
    {
        SetCollapsed(!_collapsed);
        CollapsedChanged?.Invoke(_collapsed);
    }

    public void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;
        _collapseBtn.Text = collapsed ? "▴" : "▾";
        if (collapsed)
        {
            if (ClientSize.Height > CollapsedH) _expandedSize = ClientSize;
            _graph.Visible = false;
            _presetBar.Visible = false;
            _bar.Visible = true;
            _collapseBtn.Top = 5;
            _close.Top = 5;
            MinimumSize = new Size(CollapsedW, CollapsedH);
            MaximumSize = new Size(CollapsedW, CollapsedH); // lock the compact size
            ClientSize = new Size(CollapsedW, CollapsedH);
            // set bounds AFTER the resize so the Left+Right anchor doesn't squish the bar width
            _bar.SetBounds(18, 6, CollapsedW - 18 - 60, 22);
        }
        else
        {
            _bar.Visible = false;
            _graph.Visible = true;
            _presetBar.Visible = true;
            _collapseBtn.Top = 9;
            _close.Top = 9;
            MaximumSize = Size.Empty;
            MinimumSize = new Size(260, 280);
            ClientSize = _expandedSize.Height >= 280 ? _expandedSize : new Size(300, 320);
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_collapsed)
        {
            using var grip = new SolidBrush(Color.FromArgb(110, 110, 110));
            g.FillEllipse(grip, 8, Height / 2 - 5, 3, 3);
            g.FillEllipse(grip, 8, Height / 2 + 2, 3, 3);
            return; // collapsed: just the slim bar + buttons, no header chrome
        }

        using (var path = Rounded(new RectangleF(16, 14, 13, 13), 3))
        using (var lb = new SolidBrush(Accent))
            g.FillPath(lb, path);
        using (var titleFont = new Font("Segoe UI Semibold", 10.5f))
        using (var tb = new SolidBrush(Color.White))
            g.DrawString("Spotify Volume", titleFont, tb, 36, 11);

        Color dot = _model.SessionFound ? Accent : Color.FromArgb(225, 185, 80);
        using (var dbr = new SolidBrush(dot))
            g.FillEllipse(dbr, Width - 84, 16, 9, 9);

        if (_collapsed) return;

        float gain = _model.Gain;
        double db = gain > 0 ? 20 * Math.Log10(gain) : double.NegativeInfinity;
        string pct = $"{gain * 100:0}%";
        string dbText = double.IsNegativeInfinity(db) ? "−∞ dB" : $"{db:0.0} dB";
        using (var big = new Font("Segoe UI", 20f, FontStyle.Bold))
        {
            var sz = g.MeasureString(pct, big);
            using var wb = new SolidBrush(Color.White);
            g.DrawString(pct, big, wb, (Width - sz.Width) / 2f, Height - 84);
        }
        using (var small = new Font("Segoe UI", 9f))
        {
            var sz = g.MeasureString(dbText, small);
            using var sb = new SolidBrush(Color.FromArgb(140, 140, 140));
            g.DrawString(dbText, small, sb, (Width - sz.Width) / 2f, Height - 52);
        }
    }

    private void OnModelChanged()
    {
        if (IsDisposed) return;
        _presetBar.SetActive(Array.FindIndex(_presets, x => Math.Abs(x.P - _model.P) < 0.001f));
        _graph.Set(_model.P, _model.Position);
        _bar.Set(_model.Position);
        Invalidate();
    }

    // ----- docking -----
    public void SetDockOffset(Point? offset) => _dockOffset = offset;
    public void ResetDockOffset() => _dockOffset = null;
    public void ApplyClientSize(Size size) { if (size.Width > 0 && size.Height > 0) { _expandedSize = size; if (!_collapsed) ClientSize = size; } }

    public void SetDockMode(bool on)
    {
        _dockMode = on;
        if (on) { _userHidden = false; _presenceTimer.Start(); PresenceTick(); }
        else { _presenceTimer.Stop(); UninstallHook(); }
    }

    // Low-frequency: detect Spotify presence, (re)install the move hook, hide when gone.
    private void PresenceTick()
    {
        if (!_dockMode) return;
        bool ok = SpotifyWindowTracker.TryGetBounds(_spotifyHwnd, _spotifyPid, out _);
        if (!ok)
        {
            _spotifyHwnd = SpotifyWindowTracker.FindWindow(out _spotifyPid);
            ok = SpotifyWindowTracker.TryGetBounds(_spotifyHwnd, _spotifyPid, out _);
        }
        if (ok)
        {
            if (_hookedPid != _spotifyPid) InstallHook();
            Reposition();
        }
        else
        {
            UninstallHook();
            if (Visible) Hide();
        }
    }

    // Event-driven: fires exactly when the Spotify window moves → smooth, refresh-independent.
    private void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (!_dockMode || _userDragging) return;
        if (idObject != 0 || idChild != 0 || hwnd != _spotifyHwnd) return;
        Reposition();
    }

    private void Reposition()
    {
        if (!_dockMode || _userDragging) return;
        if (!SpotifyWindowTracker.TryGetBounds(_spotifyHwnd, _spotifyPid, out var r)) return;

        _lastAnchor = new Point(r.Left, r.Top);
        _hasAnchor = true;
        var screen = Screen.FromRectangle(r).WorkingArea;
        Point target = _dockOffset is Point off
            ? new Point(r.Left + off.X, r.Top + off.Y)
            : new Point(
                r.Right + 8 + Width <= screen.Right ? r.Right + 8 : r.Right - Width - 12,
                r.Top + (r.Height - Height) / 2);
        Location = new Point(
            ClampToScreen(target.X, screen.Left + 8, screen.Right - Width - 8, screen.Left),
            ClampToScreen(target.Y, screen.Top + 8, screen.Bottom - Height - 8, screen.Top));

        IntPtr above = GetWindow(_spotifyHwnd, GW_HWNDPREV);
        if (above != Handle)
            SetWindowPos(Handle, above, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        if (!Visible && !_userHidden) Show();
    }

    private void InstallHook()
    {
        UninstallHook();
        if (_spotifyPid == 0) return;
        _winEventHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _winEventProc, _spotifyPid, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _hookedPid = _winEventHook != IntPtr.Zero ? _spotifyPid : 0;
    }

    private void UninstallHook()
    {
        if (_winEventHook != IntPtr.Zero) { UnhookWinEvent(_winEventHook); _winEventHook = IntPtr.Zero; }
        _hookedPid = 0;
    }

    private static int ClampToScreen(int v, int min, int max, int fallback) => max >= min ? Math.Clamp(v, min, max) : fallback;

    public void ShowNearTray()
    {
        _userHidden = false;
        var wa = (Screen.PrimaryScreen ?? Screen.FromControl(this)).WorkingArea;
        Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 6);
        Show();
        BringToFront();
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; _userHidden = true; Hide(); }
        else base.OnFormClosing(e);
    }

    // ----- borderless drag/resize + dock-move tracking -----
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084, WM_ENTERSIZEMOVE = 0x0231, WM_EXITSIZEMOVE = 0x0232;

        if (m.Msg == WM_NCHITTEST)
        {
            long lp = m.LParam.ToInt64();
            var pt = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            m.Result = (IntPtr)HitTest(pt);
            return;
        }
        if (m.Msg == WM_ENTERSIZEMOVE)
        {
            _userDragging = true;
        }
        else if (m.Msg == WM_EXITSIZEMOVE)
        {
            _userDragging = false;
            if (_dockMode && _hasAnchor)
            {
                _dockOffset = new Point(Left - _lastAnchor.X, Top - _lastAnchor.Y);
                DockOffsetChanged?.Invoke(_dockOffset.Value);
            }
            if (!_collapsed) PanelBoundsChanged?.Invoke(ClientSize);
        }
        base.WndProc(ref m);
    }

    private int HitTest(Point p)
    {
        const int grip = 6;
        const int HTCLIENT = 1, HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12,
                  HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        if (_collapsed)
            return p.X < 18 ? HTCAPTION : HTCLIENT; // drag by the left grip
        {
            bool l = p.X < grip, r = p.X >= ClientSize.Width - grip, t = p.Y < grip, b = p.Y >= ClientSize.Height - grip;
            if (t && l) return HTTOPLEFT;
            if (t && r) return HTTOPRIGHT;
            if (b && l) return HTBOTTOMLEFT;
            if (b && r) return HTBOTTOMRIGHT;
            if (l) return HTLEFT;
            if (r) return HTRIGHT;
            if (t) return HTTOP;
            if (b) return HTBOTTOM;
        }
        if (p.Y < HeaderH) return HTCAPTION; // drag by the header
        return HTCLIENT;
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ----- native -----
    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, GW_HWNDPREV = 3;
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B, WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 0x0002;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _presenceTimer.Stop();
            _presenceTimer.Dispose();
            UninstallHook();
        }
        base.Dispose(disposing);
    }
}
