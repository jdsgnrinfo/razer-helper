using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using RazerHelper.Core.Models;

namespace RazerHelper.UI;

/// <summary>
/// The tray icon in the color of the current performance mode: the app's
/// logo with its green disc repainted (Balanced keeps the original green) and
/// the black mark left as it is. Each color is made once, from the icon file
/// at the size the tray uses, and kept until the app exits.
/// </summary>
internal sealed class TrayIconArt : IDisposable
{
    /// <summary>The disc's color for each mode. Modes left out keep the logo's own green.</summary>
    public static readonly IReadOnlyDictionary<PerformanceMode, Color> ModeColors = new Dictionary<PerformanceMode, Color>
    {
        [PerformanceMode.Silent] = Color.FromArgb(0x00, 0x6A, 0xFF),
        [PerformanceMode.Custom] = Color.FromArgb(0xA2, 0x59, 0xFF)
    };

    private readonly Icon _original;
    private readonly Dictionary<PerformanceMode, Icon> _tinted = [];

    public TrayIconArt(Icon original)
    {
        _original = original;
    }

    /// <summary>The icon for <paramref name="mode"/>; the original for Balanced or an unknown mode.</summary>
    public Icon For(PerformanceMode? mode)
    {
        if (mode is not PerformanceMode known || !ModeColors.TryGetValue(known, out var color))
            return _original;

        if (!_tinted.TryGetValue(known, out var icon))
            _tinted[known] = icon = Tint(_original, color);

        return icon;
    }

    public void Dispose()
    {
        foreach (var icon in _tinted.Values)
            icon.Dispose();

        _tinted.Clear();
    }

    /// <summary>
    /// Repaints the logo's green in <paramref name="color"/>. A pixel's share of
    /// green (its green channel, the logo being pure green over black) sets
    /// how much of the new color it gets, so the soft edges between the disc
    /// and the black mark stay smooth; transparency is kept as it is.
    /// </summary>
    internal static Bitmap TintBitmap(Bitmap source, Color color)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);

        var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

        try
        {
            var pixels = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            for (var at = 0; at < pixels.Length; at += 4)
            {
                // Byte order in memory: blue, green, red, alpha.
                var (blue, green, red) = (pixels[at], pixels[at + 1], pixels[at + 2]);

                // Only the green parts: greener than they are red or blue.
                if (green <= red || green <= blue)
                    continue;

                var share = (green - Math.Max(red, blue)) / 255.0;
                var rest = Math.Max(red, blue);

                pixels[at] = (byte)Math.Min(255, rest + color.B * share);
                pixels[at + 1] = (byte)Math.Min(255, rest + color.G * share);
                pixels[at + 2] = (byte)Math.Min(255, rest + color.R * share);
            }

            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private static Icon Tint(Icon original, Color color)
    {
        using var source = original.ToBitmap();
        using var tinted = TintBitmap(source, color);

        // FromHandle does not own the handle, so keep a copy and free it.
        var handle = tinted.GetHicon();

        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
