using System.Runtime.InteropServices;

namespace RazerHelper.Helpers;

/// <summary>
/// Windows 11 window styling for a borderless popup: rounded corners and a
/// thin border in a chosen color, or none. Both are best-effort; on Windows 10 the DWM
/// rejects the attributes and the popup simply keeps square corners.
/// </summary>
public static class WindowChrome
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmBorderColor = 34;
    private const int RoundedCorners = 2;
    private const int NoBorder = unchecked((int)0xFFFFFFFE);

    public static void Apply(IntPtr windowHandle, Color? borderColor)
    {
        var corner = RoundedCorners;
        DwmSetWindowAttribute(windowHandle, DwmWindowCornerPreference, ref corner, sizeof(int));

        // COLORREF is 0x00BBGGRR, not the usual RGB order.
        var border = borderColor is { } color ? color.R | (color.G << 8) | (color.B << 16) : NoBorder;
        DwmSetWindowAttribute(windowHandle, DwmBorderColor, ref border, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}
