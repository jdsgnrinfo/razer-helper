namespace RazerHelper.Core.Models;

/// <summary>
/// Performance modes Razer Synapse offers on the Blade 16 (2023), plus the
/// 2020 Blades' Gaming, offered on every model. The values are the EC's wire
/// bytes. The EC knows a few more (Battery, Hyperboost) that are deliberately
/// left out.
/// </summary>
internal enum PerformanceMode : byte
{
    Balanced = 0,

    // The 2020 Blades' high-performance mode (Synapse called it Gaming).
    // Offered on every model; one whose firmware does not take it reports
    // another mode back, and the button then says it is not supported.
    Gaming = 1,

    Custom = 4,
    Silent = 5
}
