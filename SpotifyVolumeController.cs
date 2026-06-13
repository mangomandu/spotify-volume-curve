using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace SpotifyLinearVolume;

/// <summary>
/// Controls Spotify's per-application audio session volume (the Windows volume-mixer
/// level for Spotify.exe) via Core Audio. This is stage 2 — completely separate from
/// Spotify's own internal volume, so it never touches the client and survives updates.
/// Set Spotify's own slider to 100% so this becomes the real volume knob.
/// </summary>
public sealed class SpotifyVolumeController : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public bool IsSpotifyRunning
    {
        get
        {
            var procs = Process.GetProcessesByName("Spotify");
            try { return procs.Length > 0; }
            finally { foreach (var p in procs) p.Dispose(); }
        }
    }

    /// <summary>Set every Spotify session on the default render device to the given gain (0..1).</summary>
    public bool SetGain(float gain)
    {
        gain = Math.Clamp(gain, 0f, 1f);
        try
        {
            bool any = false;
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                if (IsSpotify(s))
                {
                    s.SimpleAudioVolume.Volume = gain;
                    any = true;
                }
            }
            return any;
        }
        catch (COMException)
        {
            // No active render device / audio service hiccup / device changed mid-call.
            return false;
        }
    }

    /// <summary>Current gain of the first Spotify session, or null if none.</summary>
    public float? GetGain()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                if (IsSpotify(s))
                    return s.SimpleAudioVolume.Volume;
            }
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static bool IsSpotify(AudioSessionControl session)
    {
        try
        {
            uint pid = session.GetProcessID;
            if (pid == 0) return false;
            using var p = Process.GetProcessById((int)pid);
            return string.Equals(p.ProcessName, "Spotify", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _enumerator.Dispose();
}
