using System.Runtime.InteropServices;

namespace RazerHelper.Helpers;

/// <summary>
/// Tells when the screen turns off and on, from Windows' console display state
/// notice (GUID_CONSOLE_DISPLAY_STATE): the display timeout, sleep and modern
/// standby, and a closed lid all send it. Needs only a hidden message window;
/// no polling.
/// </summary>
internal sealed class DisplayStateWatcher : NativeWindow, IDisposable
{
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtPowerSettingChange = 0x8013;
    private const int DeviceNotifyWindowHandle = 0;

    private static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    private IntPtr _registration;

    /// <summary>True when the screen came on, false when it went off. Raised on the UI thread.</summary>
    public event EventHandler<bool>? DisplayChanged;

    public DisplayStateWatcher()
    {
        CreateHandle(new CreateParams());

        var guid = ConsoleDisplayState;
        _registration = RegisterPowerSettingNotification(Handle, ref guid, DeviceNotifyWindowHandle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmPowerBroadcast && m.WParam.ToInt32() == PbtPowerSettingChange && m.LParam != IntPtr.Zero)
        {
            // POWERBROADCAST_SETTING: the setting's GUID, the data length, then
            // the data: 0 off, 1 on, 2 dimmed (still on).
            var setting = Marshal.PtrToStructure<Guid>(m.LParam);

            if (setting == ConsoleDisplayState)
            {
                var state = Marshal.ReadInt32(m.LParam, 20);
                DisplayChanged?.Invoke(this, state != 0);
            }
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registration != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_registration);
            _registration = IntPtr.Zero;
        }

        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid powerSettingGuid, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
}
