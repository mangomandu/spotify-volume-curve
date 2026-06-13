using System.Runtime.InteropServices;

namespace SpotifyLinearVolume;

/// <summary>
/// Registers global hotkeys via user32 RegisterHotKey and dispatches WM_HOTKEY
/// through a hidden message window.
/// </summary>
public sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [Flags]
    private enum Mod : uint { Alt = 1, Control = 2, Shift = 4, Win = 8, NoRepeat = 0x4000 }

    private int _idCounter;
    private readonly Dictionary<int, Action> _actions = new();

    public HotkeyManager() => CreateHandle(new CreateParams());

    public bool Register(uint vk, Action action, bool ctrl = true, bool alt = true, bool shift = false)
    {
        uint mods = (uint)Mod.NoRepeat;
        if (ctrl) mods |= (uint)Mod.Control;
        if (alt) mods |= (uint)Mod.Alt;
        if (shift) mods |= (uint)Mod.Shift;

        int id = ++_idCounter;
        if (RegisterHotKey(Handle, id, mods, vk))
        {
            _actions[id] = action;
            return true;
        }
        return false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && _actions.TryGetValue(m.WParam.ToInt32(), out var action))
            action();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        for (int id = 1; id <= _idCounter; id++)
            UnregisterHotKey(Handle, id);
        DestroyHandle();
    }
}
