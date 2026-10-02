using System.Runtime.InteropServices;
using RazerHelper.Core.Services;

namespace RazerHelper.Helpers;

/// <summary>
/// The time since the last key press or mouse move (GetLastInputInfo), and
/// whether a program is keeping the screen on (the system's execution state),
/// which is how Windows itself decides not to turn the screen off.
/// </summary>
internal sealed class WindowsIdleClock : IIdleClock
{
    private const int SystemExecutionState = 16;
    private const uint DisplayRequired = 0x2;

    public TimeSpan SinceLastInput()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };

        if (!GetLastInputInfo(ref info))
            return TimeSpan.Zero; // Unknown: count it as being there.

        // Both are milliseconds since startup in 32 bits, so the difference survives the wrap.
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
    }

    public bool ScreenKeptOn() =>
        CallNtPowerInformation(SystemExecutionState, IntPtr.Zero, 0, out var state, sizeof(uint)) == 0 &&
        (state & DisplayRequired) != 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(int level, IntPtr input, uint inputSize, out uint output, uint outputSize);
}
