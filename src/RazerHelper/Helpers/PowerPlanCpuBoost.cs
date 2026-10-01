using System.ComponentModel;
using System.Runtime.InteropServices;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;

namespace RazerHelper.Helpers;

/// <summary>
/// Reads and writes the "processor performance boost mode" of Windows' power
/// plans through the power API. Plan settings belong to the user, so no
/// administrator rights are needed.
/// </summary>
internal sealed class PowerPlanCpuBoost : ICpuBoostSetting
{
    // GUID_PROCESSOR_SETTINGS_SUBGROUP and GUID_PROCESSOR_PERF_BOOST_MODE.
    private static readonly Guid ProcessorGroup = new("54533251-82be-4824-96c1-47b60b740d00");
    private static readonly Guid BoostMode = new("be337238-0d82-4146-a960-4f3749d470c7");

    public SavedCpuBoost Read()
    {
        var scheme = ActiveScheme();
        var group = ProcessorGroup;
        var boost = BoostMode;

        Check(PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref group, ref boost, out var pluggedIn), "read the plugged-in boost mode");
        Check(PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref group, ref boost, out var onBattery), "read the on-battery boost mode");

        return new SavedCpuBoost(scheme, pluggedIn, onBattery);
    }

    public void Write(Guid scheme, uint pluggedIn, uint onBattery)
    {
        var group = ProcessorGroup;
        var boost = BoostMode;

        Check(PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref group, ref boost, pluggedIn), "write the plugged-in boost mode");
        Check(PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref group, ref boost, onBattery), "write the on-battery boost mode");

        // A plan's new values only take effect once it is made active again.
        if (ActiveScheme() == scheme)
            Check(PowerSetActiveScheme(IntPtr.Zero, ref scheme), "apply the power plan");
    }

    private static Guid ActiveScheme()
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

    private static void Check(uint result, string what)
    {
        if (result != 0)
            throw new Win32Exception((int)result, $"Could not {what}.");
    }

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr activeScheme);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, uint value);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
