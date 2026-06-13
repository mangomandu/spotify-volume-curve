namespace SpotifyLinearVolume;

/// <summary>
/// Shared volume state (position + curve p) and the single source of truth that
/// the tray menu, hotkeys, OSD and the control panel all read from and mutate.
/// Raising <see cref="Changed"/> keeps every surface in sync.
/// </summary>
public sealed class VolumeModel : IDisposable
{
    private readonly SpotifyVolumeController _controller = new();

    public float Position { get; private set; } = 0.5f;
    public float P { get; private set; } = 0.5f;
    public bool SessionFound { get; private set; }

    public float Gain => VolumeCurve.Gain(Position, P);

    public event Action? Changed;

    public VolumeModel(float initialP)
    {
        P = float.IsFinite(initialP) && initialP > 0f ? initialP : 0.5f;
        var g = _controller.GetGain();
        if (g.HasValue)
        {
            Position = VolumeCurve.PositionFromGain(g.Value, P);
            SessionFound = true;
        }
    }

    public void Nudge(float delta) => SetPosition(Position + delta);

    public void SetPosition(float position)
    {
        Position = float.IsFinite(position) ? Math.Clamp(position, 0f, 1f) : 0f;
        Apply();
    }

    public void SetP(float p)
    {
        // Guard against non-finite / <= 0 p so the stored P (read directly by the
        // graph painter) can never produce NaN/Infinity points.
        p = float.IsFinite(p) && p > 0f ? p : 1f;

        // Keep the resulting gain continuous so the volume doesn't jump when the
        // curve strength changes — only the shape (and where the marker sits) moves.
        float gain = VolumeCurve.Gain(Position, P);
        P = p;
        Position = VolumeCurve.PositionFromGain(gain, P);
        Apply();
    }

    private void Apply()
    {
        SessionFound = _controller.SetGain(Gain);
        Changed?.Invoke();
    }

    public void Dispose() => _controller.Dispose();
}
