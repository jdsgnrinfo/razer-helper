namespace RazerHelper.Core.Models;

/// <summary>What the CPU's boost setting was before Silent turned it off, per power source, and in which power plan.</summary>
internal sealed record SavedCpuBoost(Guid Scheme, uint PluggedIn, uint OnBattery);
