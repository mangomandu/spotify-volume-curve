namespace SpotifyLinearVolume;

/// <summary>
/// Perceptual volume curve: maps a UI slider position (0..1) to an actual gain (0..1)
/// via gain = position ^ p.  p &lt; 1 spreads the audible range toward the low end and
/// makes the top of the slider gentler (tames Spotify's top-heavy native curve).
/// </summary>
public static class VolumeCurve
{
    public static float Gain(float position, float p)
    {
        p = SanitizeP(p);
        position = Math.Clamp(position, 0f, 1f);
        return (float)Math.Pow(position, p);
    }

    public static float PositionFromGain(float gain, float p)
    {
        p = SanitizeP(p);
        gain = Math.Clamp(gain, 0f, 1f);
        if (gain <= 0f) return 0f;
        return (float)Math.Pow(gain, 1.0 / p);
    }

    // Guard against p &lt;= 0 / NaN / Infinity, which would invert or break the curve math.
    private static float SanitizeP(float p) => float.IsFinite(p) && p > 0f ? p : 1f;
}
