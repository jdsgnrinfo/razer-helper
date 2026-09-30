using System.Drawing.Text;
using System.Runtime.InteropServices;
using RazerHelper.Core.Diagnostics;

namespace RazerHelper.UI;

/// <summary>
/// The interface's typeface, Titillium Web, loaded from inside the exe for
/// this program only: nothing is installed in Windows. Regular and Bold make
/// up the family the interface uses; Light and SemiBold come along as families
/// of their own ("Titillium Web Light", "Titillium Web SemiBold").
/// </summary>
internal static class AppFonts
{
    /// <summary>The family the interface uses: its Regular and Bold.</summary>
    public const string Family = "Titillium Web";

    /// <summary>The semi-bold weight, loaded as a family of its own.</summary>
    public const string SemiBoldFamily = "Titillium Web SemiBold";

    private static readonly string[] Files =
    [
        "TitilliumWeb-Regular.ttf",
        "TitilliumWeb-Bold.ttf",
        "TitilliumWeb-Light.ttf",
        "TitilliumWeb-SemiBold.ttf"
    ];

    // Kept for the life of the program: fonts made from it stay tied to it.
    private static readonly PrivateFontCollection Collection = Load();

    /// <summary>Whether the interface's family loaded; without it the interface falls back to Windows' own font.</summary>
    public static bool IsLoaded => Find(Family) is not null;

    /// <summary>The loaded family of that name, or null when it is not one of ours.</summary>
    public static FontFamily? Find(string name) =>
        Collection.Families.FirstOrDefault(family => string.Equals(family.Name, name, StringComparison.OrdinalIgnoreCase));

    // Each file is handed to both of Windows' text engines, since the
    // interface draws with both: the collection serves Font objects, and the
    // process-wide registration serves TextRenderer. The memory is never
    // freed, as both keep reading from it.
    private static PrivateFontCollection Load()
    {
        var collection = new PrivateFontCollection();

        foreach (var file in Files)
        {
            try
            {
                using var stream = typeof(AppFonts).Assembly.GetManifestResourceStream($"Fonts.{file}");

                if (stream is null)
                    continue;

                var bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);

                var memory = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, memory, bytes.Length);

                collection.AddMemoryFont(memory, bytes.Length);

                uint added = 0;
                AddFontMemResourceEx(memory, (uint)bytes.Length, IntPtr.Zero, ref added);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or ExternalException)
            {
                AppLog.Error($"Could not load the font {file}.", exception);
            }
        }

        return collection;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr font, uint size, IntPtr reserved, ref uint count);
}
