namespace RazerHelper.Core.Models;

/// <summary>
/// Performance modes Razer Synapse offers on the Blade 16 (2023), plus Gaming
/// for the models that have it. The values are the EC's wire bytes. The EC
/// knows a few more (Battery, Hyperboost) that are deliberately left out.
/// </summary>
internal enum PerformanceMode : byte
{
    Balanced = 0,

    // The 2020 Blades' high-performance mode (Synapse called it Gaming). Only
    // offered where RazerLaptopModel.HasGamingMode says so.
    Gaming = 1,

    Custom = 4,
    Silent = 5
}
