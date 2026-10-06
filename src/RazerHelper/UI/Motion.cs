namespace RazerHelper.UI;

/// <summary>
/// The timing every small transition shares (hover, switches): 300 ms, easing
/// in and out, slow at both ends, so changes glide rather than snap.
/// </summary>
internal static class Motion
{
    public const double Milliseconds = 300;

    /// <summary>
    /// Whether Windows' "Animation effects" is off: then the sliders' springs,
    /// stretch and rolling digits give way to instant changes.
    /// </summary>
    public static bool Reduced => SystemParametersInfo(GetClientAreaAnimation, 0, out var on, 0) && !on;

    private const uint GetClientAreaAnimation = 0x1042;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint param, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] out bool value, uint winIni);

    /// <summary>Eases a linear 0..1 progress in and out (cubic).</summary>
    public static float Ease(float progress)
    {
        var t = Math.Clamp(progress, 0f, 1f);
        return t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
    }

    /// <summary>A color partway from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)Math.Round(from.A + (to.A - from.A) * amount),
        (int)Math.Round(from.R + (to.R - from.R) * amount),
        (int)Math.Round(from.G + (to.G - from.G) * amount),
        (int)Math.Round(from.B + (to.B - from.B) * amount));
}
