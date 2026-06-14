using Microsoft.Win32;

namespace SpotifyLinearVolume;

/// <summary>Toggles "run at Windows login" via the HKCU Run key.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SpotifyLinearVolume";

    private static string ExePath => Environment.ProcessPath ?? Application.ExecutablePath;

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            // Only "enabled" if the stored path points at THIS executable; a stale path
            // from an old build reads as off so enabling rewrites it to the current exe.
            return key?.GetValue(ValueName) is string s
                && string.Equals(s.Trim('"'), ExePath, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;
            if (enabled) key.SetValue(ValueName, $"\"{ExePath}\"");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* best effort — registry may be locked down */ }
    }
}
