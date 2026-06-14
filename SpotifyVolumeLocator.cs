using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SpotifyLinearVolume;

/// <summary>
/// Locates Spotify's native volume slider for the overlay, using only UI Automation.
///
/// Spotify is Chromium; UIA tells the truth about <b>X</b> (tracks the slider/buttons to ~1px) and,
/// for the real volume slider, about <b>Y</b> when it sits in the playbar. UIA's <i>Width</i> is the
/// hit‑area (~129px), wider than the drawn rail (~92px), so we trim it; and we clamp the right edge
/// to the nearest mini‑player / fullscreen button (found by accessible name) so the overlay never
/// spills onto them as the rail compresses at narrow widths. The controller blurs the slider right
/// after writing to it, so it stays in its resting state (no focus ring) under our overlay.
/// </summary>
public static class SpotifyVolumeLocator
{
    private const int SliderHeight = 20;           // a touch taller than the ~16px rail, for easier grabbing
    private const int RailRightCover = 6;          // extend past the rail's right end: we drive the slider, so its
                                                   // white fill/knob span the whole track and must be hidden
    private const int RailLeftCover = 4;           // cover the rail's left cap (gap to the speaker icon absorbs it)
    private const int ButtonGap = 6;               // keep this clear of the next button
    private const int WindowEdgeGap = 8;           // never poke past the window's right edge
    private const int GeometricCenterOffset = 46;  // fallback: rail centre this far above the window bottom

    // Accessible-name fragments for the controls immediately right of the volume rail (mini-player /
    // fullscreen) in the locales we support — used to clamp the overlay's right edge.
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
                if (bx > x + 10 && bx < nearestButtonX) nearestButtonX = bx;
            }

            if (!GetWindowRect(spotifyHwnd, out var win))
                return new Rectangle(x - RailLeftCover, (int)r.Y,
                                     (int)r.Width + RailLeftCover + RailRightCover, (int)r.Height);

            // Cover the whole rail (left cap to past the knob), then clamp the right edge clear of the next
            // button and the window edge so it never spills onto them.
            int left = x - RailLeftCover;
            int right = x + (int)r.Width + RailRightCover;
            if (nearestButtonX != int.MaxValue) right = Math.Min(right, nearestButtonX - ButtonGap);
            right = Math.Min(right, win.Right - WindowEdgeGap);
            int width = Math.Max(24, right - left);

            // Vertical centre: trust UIA's Y when it lands in the playbar region (reliable for the real
            // volume slider, robust to DPI / maximized vs floating); otherwise fall back to a fixed offset.
            int uiaCenterY = (int)(r.Y + r.Height / 2);
            int centerY = (uiaCenterY >= win.Bottom - 100 && uiaCenterY <= win.Bottom - 18)
                ? uiaCenterY
                : win.Bottom - GeometricCenterOffset;

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
