using System.Runtime.InteropServices;
using RazerHelper.Core.Models;

namespace RazerHelper.Helpers;

/// <summary>
/// The shortcuts that switch performance mode from any program, including a
/// game: Ctrl+Shift+F1 Balanced, F2 Silent, F3 Gaming. Like
/// <see cref="GlobalHotkey"/>, Windows delivers them to a hidden window of
/// ours, so nothing watches the keyboard. (Fn+1/2/3 cannot be used: the
/// Blade's Fn key never reaches Windows, so Fn+1 looks exactly like 1.)
/// </summary>
internal sealed class ProfileShortcuts : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    // Ids apart from GlobalHotkey's, though each window has its own anyway.
    private const int FirstId = 100;

    private static readonly IntPtr MessageOnlyParent = new(-3);

    /// <summary>Each shortcut: its mode, key and name in the interface.</summary>
    public static readonly (PerformanceMode Mode, Keys Key, string Text)[] All =
    [
        (PerformanceMode.Balanced, Keys.F1, "Ctrl+Shift+F1"),
        (PerformanceMode.Silent, Keys.F2, "Ctrl+Shift+F2"),
        (PerformanceMode.Gaming, Keys.F3, "Ctrl+Shift+F3")
    ];

    private readonly HashSet<int> _registered = [];

    /// <summary>Raised with the mode asked for, on the UI thread.</summary>
    public event EventHandler<PerformanceMode>? Pressed;

    /// <summary>Starts listening. Returns the shortcuts another program already owns.</summary>
    public IReadOnlyList<string> TryRegister()
    {
        if (Handle == IntPtr.Zero)
            CreateHandle(new CreateParams { Parent = MessageOnlyParent });

        var taken = new List<string>();

        for (var index = 0; index < All.Length; index++)
        {
            var id = FirstId + index;

            if (_registered.Contains(id))
                continue;

            if (RegisterHotKey(Handle, id, ModControl | ModShift | ModNoRepeat, (uint)All[index].Key))
                _registered.Add(id);
            else
                taken.Add(All[index].Text);
        }

        return taken;
    }

    protected override void WndProc(ref Message message)
    {
        var index = (int)message.WParam - FirstId;

        if (message.Msg == WmHotkey && index >= 0 && index < All.Length)
            Pressed?.Invoke(this, All[index].Mode);
        else
            base.WndProc(ref message);
    }

    public void Dispose()
    {
        foreach (var id in _registered)
            UnregisterHotKey(Handle, id);

        _registered.Clear();

        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
