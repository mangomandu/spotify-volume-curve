using System.Runtime.InteropServices;

namespace Volumify;

/// <summary>
/// Makes a borderless Form dock to (follow) the Spotify window — the same approach the control panel uses,
/// pulled out so the lyrics window can reuse it. Tracks Spotify with a low-frequency presence timer plus an
/// EVENT_OBJECT_LOCATIONCHANGE hook (event-driven, so it keeps up with any window move/resize at any refresh
/// rate). The owner forwards drag start/end from its WndProc so a manual drag re-anchors the saved offset.
///
/// No z-order juggling here: the lyrics window is TopMost, so it already sits above Spotify — we only move it.
/// </summary>
public sealed class SpotifyDock : IDisposable
{
    private readonly Form _form;
    private readonly Func<Rectangle, Size, Point> _defaultPlacement; // (spotifyBounds, formSize) → top-left when no saved offset
    private readonly System.Windows.Forms.Timer _presence = new() { Interval = 250 };
    private readonly WinEventProc _winEventProc;

    private IntPtr _hook;
    private uint _hookedPid;
    private IntPtr _hwnd;
    private uint _pid;
    private bool _enabled, _dragging, _hasAnchor;
    private Rectangle _lastSpotify;
    private Point? _offset;

    /// <summary>User dragged the window → new offset relative to Spotify's top-left (persist this).</summary>
    public event Action<Point>? OffsetChanged;

    public SpotifyDock(Form form, Func<Rectangle, Size, Point> defaultPlacement)
    {
        _form = form;
        _defaultPlacement = defaultPlacement;
        _winEventProc = OnWinEvent;
        _presence.Tick += (_, _) => PresenceTick();
    }

    public void SetOffset(Point? offset) => _offset = offset;

    public void SetEnabled(bool on)
    {
        if (_enabled == on) return;
        _enabled = on;
        if (on) { _presence.Start(); PresenceTick(); }
        else { _presence.Stop(); UninstallHook(); _hasAnchor = false; }
    }

    public void NotifyDragStart() { if (_enabled) _dragging = true; }

    public void NotifyDragEnd()
    {
        if (!_enabled) return;
        _dragging = false;
        if (_hasAnchor)
        {
            // store relative to Spotify's bottom-right corner so resizing Spotify moves the window too
            _offset = new Point(_form.Left - _lastSpotify.Right, _form.Top - _lastSpotify.Bottom);
            OffsetChanged?.Invoke(_offset.Value);
        }
    }

    // Low-frequency: detect Spotify presence, (re)install the move hook, and show/hide the window with Spotify —
    // it follows Spotify while visible and hides when Spotify is minimized/closed.
    private void PresenceTick()
    {
        if (!_enabled) return;
        bool ok = SpotifyWindowTracker.TryGetBounds(_hwnd, _pid, out _);
        if (!ok)
        {
            _hwnd = SpotifyWindowTracker.FindWindow(out _pid);
            ok = SpotifyWindowTracker.TryGetBounds(_hwnd, _pid, out _);
        }
        if (ok)
        {
            if (_hookedPid != _pid) InstallHook();
            if (!_form.Visible) _form.Show();   // Spotify visible → show beside it
            Reposition();
        }
        else
        {
            UninstallHook(); _hasAnchor = false;
            if (_form.Visible) _form.Hide();     // Spotify minimized/closed → hide with it
        }
    }

    // Event-driven: fires exactly when the Spotify window moves → smooth, refresh-independent.
    private void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (!_enabled || _dragging) return;
        if (idObject != 0 || idChild != 0 || hwnd != _hwnd) return;
        Reposition();
    }

    public void Reposition()
    {
        if (!_enabled || _dragging || !_form.IsHandleCreated) return;
        if (!SpotifyWindowTracker.TryGetBounds(_hwnd, _pid, out var r)) return;

        _lastSpotify = r;
        _hasAnchor = true;
        var screen = Screen.FromRectangle(r).WorkingArea;
        // Anchor to Spotify's bottom-right corner → the window follows when Spotify is *resized*
        // (its right/bottom edges move), not only when it's dragged.
        Point target = _offset is Point off
            ? new Point(r.Right + off.X, r.Bottom + off.Y)
            : _defaultPlacement(r, _form.Size);
        _form.Location = new Point(
            Clamp(target.X, screen.Left + 8, screen.Right - _form.Width - 8, screen.Left),
            Clamp(target.Y, screen.Top + 8, screen.Bottom - _form.Height - 8, screen.Top));
    }

    private void InstallHook()
    {
        UninstallHook();
        if (_pid == 0) return;
        _hook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _winEventProc, _pid, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        _hookedPid = _hook != IntPtr.Zero ? _pid : 0;
    }

    private void UninstallHook()
    {
        if (_hook != IntPtr.Zero) { UnhookWinEvent(_hook); _hook = IntPtr.Zero; }
        _hookedPid = 0;
    }

    private static int Clamp(int v, int min, int max, int fallback) => max >= min ? Math.Clamp(v, min, max) : fallback;

    public void Dispose() { _presence.Stop(); _presence.Dispose(); UninstallHook(); }

    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B, WINEVENT_OUTOFCONTEXT = 0, WINEVENT_SKIPOWNPROCESS = 0x0002;
}
