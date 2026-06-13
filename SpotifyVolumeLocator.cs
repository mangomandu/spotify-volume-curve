using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SpotifyLinearVolume;

/// <summary>
/// Locates Spotify's native volume slider for the overlay.
///
/// Spotify is Chromium and UI Automation lies about two of the four numbers, verified by
/// screen-capturing the real window (PrintWindow) across several widths:
///  • X (left edge) is RELIABLE — it matches the drawn rail to ~1px and tracks resizes.
///  • Width is INFLATED: UIA reports ~129px of slider hit-area but the rail is only ~92px wide;
///    the extra ~37px spills right, over the mini-player button next to the slider. So we trim
///    <see cref="RailRightInset"/> off the width to get the visually-drawn rail and stop covering
///    that button.
///  • Y is UNRELIABLE (comes back ~window.Bottom+47, off-screen). We ignore it and anchor the
///    rail to the playbar geometrically from the window's bottom edge (GetWindowRect is reliable).
/// PlaybarSliderOffset and RailRightInset are tunable; both were calibrated from PrintWindow scans.
/// </summary>
public static class SpotifyVolumeLocator
{
    private const int PlaybarSliderOffset = 54; // slider top above the Spotify window's bottom edge
    private const int SliderHeight = 16;
    private const int RailRightInset = 37;      // UIA width overshoots the drawn rail by ~37px (hit-padding)

    public static Rectangle? FindVolumeRect(IntPtr spotifyHwnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(spotifyHwnd);
            if (root == null) return null;

            var sliders = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Slider));

            foreach (AutomationElement s in sliders)
            {
                string name = s.Current.Name ?? "";
                if (name.Contains("볼륨") || name.Contains("Volume", StringComparison.OrdinalIgnoreCase))
                {
                    var r = s.Current.BoundingRectangle;
                    if (r.IsEmpty || r.Width < 1) continue;

                    int x = (int)r.X;
                    int w = Math.Max(24, (int)r.Width - RailRightInset); // trim hit-padding to the drawn rail
                    // X from UIA (correct, tracks resize); Width trimmed; Y from the playbar (window bottom).
                    if (GetWindowRect(spotifyHwnd, out var win))
                        return new Rectangle(x, win.Bottom - PlaybarSliderOffset, w, SliderHeight);

                    return new Rectangle(x, (int)r.Y, w, (int)r.Height);
                }
            }
            return null;
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
