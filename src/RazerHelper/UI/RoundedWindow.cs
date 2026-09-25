using System.Runtime.InteropServices;

namespace RazerHelper.UI;

/// <summary>
/// Asks Windows 11 to draw a window (here, a drop-down list) with small
/// rounded corners, 4px, and no outline of its own. Windows 10 does not know
/// these settings and simply leaves the window square, so a failure is ignored.
/// </summary>
internal static class RoundedWindow
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpRoundSmall = 3;
    private const uint DwmwaColorNone = 0xFFFFFFFE;

    public static void Apply(IntPtr handle)
    {
        var corner = DwmwcpRoundSmall;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        var border = DwmwaColorNone;
        _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, sizeof(uint));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint value, int size);
}
