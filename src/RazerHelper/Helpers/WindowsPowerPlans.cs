using System.ComponentModel;
using System.Runtime.InteropServices;
using RazerHelper.Core.Services;

namespace RazerHelper.Helpers;

/// <summary>
/// Windows' power plans through the power API, as the Control Panel lists
/// them. Choosing the active plan is the user's own setting, so no
/// administrator rights are needed.
/// </summary>
internal sealed class WindowsPowerPlans : IPowerPlans
{
    private const uint AccessScheme = 16;
    private const uint NoMoreItems = 259;

    public IReadOnlyList<PowerPlan> List()
    {
        var found = new List<PowerPlan>();
        var buffer = new byte[16];

        for (uint index = 0; ; index++)
        {
            var size = (uint)buffer.Length;
            var result = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, AccessScheme, index, buffer, ref size);

            if (result == NoMoreItems)
                break;

            Check(result, "list the power plans");
            var plan = new Guid(buffer);
            found.Add(new PowerPlan(plan, FriendlyName(plan) ?? plan.ToString()));
        }

        return found;
    }

    public Guid Active()
    {
        Check(PowerGetActiveScheme(IntPtr.Zero, out var pointer), "find the active power plan");

        try
        {
            return Marshal.PtrToStructure<Guid>(pointer);
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    public void Activate(Guid plan) =>
        Check(PowerSetActiveScheme(IntPtr.Zero, ref plan), "change the power plan");

    private static string? FriendlyName(Guid plan)
    {
        uint size = 0;

        if (PowerReadFriendlyName(IntPtr.Zero, ref plan, IntPtr.Zero, IntPtr.Zero, null, ref size) != 0 || size == 0)
            return null;

        var name = new byte[size];

        if (PowerReadFriendlyName(IntPtr.Zero, ref plan, IntPtr.Zero, IntPtr.Zero, name, ref size) != 0)
            return null;

        var text = System.Text.Encoding.Unicode.GetString(name).TrimEnd('\0').Trim();
        return text.Length > 0 ? text : null;
    }

    private static void Check(uint result, string what)
    {
        if (result != 0)
            throw new Win32Exception((int)result, $"Could not {what}.");
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerEnumerate(IntPtr rootKey, IntPtr scheme, IntPtr subgroup, uint accessFlags, uint index, byte[] buffer, ref uint bufferSize);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadFriendlyName(IntPtr rootKey, ref Guid scheme, IntPtr subgroup, IntPtr setting, byte[]? buffer, ref uint bufferSize);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr rootKey, out IntPtr scheme);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr rootKey, ref Guid scheme);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
