using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SpotifyLinearVolume;

/// <summary>
/// Locates Spotify's native volume slider for the overlay, using only UI Automation.
///
/// Spotify is Chromium and UIA only tells the truth about <b>X</b> coordinates — it tracks the
/// slider's and buttons' left edges to ~1px across resizes. Two things it lies about:
///  • The volume slider's <b>BoundingRectangle</b> is a fixed ~129px hit‑area that also covers the
///    speaker / mute icons sitting to its left; the actual <i>draggable rail</i> starts ~60px in
///    (past those icons) and runs to just before the mini‑player button. So we inset the left and
///    reach the right edge over to the next button instead of trusting the raw box.
///  • Every playbar element's <b>Y</b> is unreliable (off‑screen / inconsistent), so the vertical
///    centre is a tuned offset from the window's bottom edge (stable even though UIA's Y isn't).
///
/// The hit‑area is a constant width at every window size (only its position moves), so the fixed
/// insets hold across resolutions. The right edge is clamped to the nearest mini‑player / fullscreen
/// button (found by accessible name, X reliable) so the overlay never spills onto them when narrow.
/// </summary>
public static class SpotifyVolumeLocator
{
    private const int SliderHeight = 20;          // a bit taller than the rail so it fully hides it vertically
    private const int RailLeftInset = 62;         // skip the speaker / mute icons that share the slider's left hit-area
    private const int RailRightPad = 40;          // reach past the hit-area's right toward the rail end (then button-clamped)
    private const int ButtonGap = 6;              // keep this clear of the next (mini-player / fullscreen) button
    private const int WindowEdgeGap = 8;          // never poke past the window's right edge
    private const int PlaybarCenterOffset = 43;   // rail centre this far above the window's bottom edge

    // Accessible-name fragments for the controls immediately right of the volume rail
    // (mini-player / fullscreen) in the locales we support. Used to bound the overlay's right edge.
    private static readonly string[] RightButtonNames =
    {
        "전체 화면", "전체화면", "미니플레이어", "미니 플레이어",
        "full screen", "fullscreen", "mini player", "miniplayer",
    };

    public static Rectangle? FindVolumeRect(IntPtr spotifyHwnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(spotifyHwnd);
            if (root == null) return null;

            // One descendants walk for both sliders and buttons (the tree is large; don't walk twice).
            var elements = root.FindAll(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Slider),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));

            AutomationElement? volume = null;
            foreach (AutomationElement e in elements)
            {
                if (e.Current.ControlType != ControlType.Slider) continue;
                string name = e.Current.Name ?? "";
                if (name.Contains("볼륨") || name.Contains("Volume", StringComparison.OrdinalIgnoreCase))
                {
                    var rr = e.Current.BoundingRectangle;
                    if (!rr.IsEmpty && rr.Width >= 1) { volume = e; break; }
                }
            }
            if (volume == null) return null;

            var r = volume.Current.BoundingRectangle;
            int x = (int)r.X;

            // Nearest mini-player / fullscreen button to the right of the slider (by name; X is reliable).
            int nearestButtonX = int.MaxValue;
            foreach (AutomationElement e in elements)
            {
                if (e.Current.ControlType != ControlType.Button) continue;
                string name = e.Current.Name ?? "";
                if (name.Length == 0) continue;
                bool match = false;
                foreach (var key in RightButtonNames)
                    if (name.Contains(key, StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                if (!match) continue;
                var bb = e.Current.BoundingRectangle;
                if (bb.IsEmpty) continue;
                int bx = (int)bb.X;
                if (bx > x + RailLeftInset && bx < nearestButtonX) nearestButtonX = bx;
            }

            // Left edge sits past the speaker/mute icons; right edge reaches the rail's end, then is
            // clamped clear of the next button and the window edge.
            int left = x + RailLeftInset;
            int right = x + (int)r.Width + RailRightPad;

            if (!GetWindowRect(spotifyHwnd, out var win))
                return new Rectangle(left, (int)r.Y, Math.Max(24, right - left), (int)r.Height);

            if (nearestButtonX != int.MaxValue) right = Math.Min(right, nearestButtonX - ButtonGap);
            right = Math.Min(right, win.Right - WindowEdgeGap);
            int width = Math.Max(24, right - left);

            // Vertical centre: UIA's Y is unreliable in the playbar, so anchor to the window bottom.
            int centerY = win.Bottom - PlaybarCenterOffset;
            return new Rectangle(left, centerY - SliderHeight / 2, width, SliderHeight);
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
