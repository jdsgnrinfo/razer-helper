using System.Globalization;
using RazerHelper.Core.Hardware;

namespace RazerHelper.Core.Services;

/// <summary>
/// Turns the battery driver's raw figures into the lines of the Battery
/// details window. Pure, so every line can be tested without a battery.
/// </summary>
internal static class BatteryDetailsText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>The window's rows, label and value, in order. Rows the battery gives no figure for are left out.</summary>
    public static IReadOnlyList<(string Label, string Value)> Rows(BatteryDetails battery)
    {
        var rows = new List<(string, string)> { ("Status", Status(battery)) };

        if (Power(battery) is { } power)
            rows.Add(("Power", power));

        if (Time(battery) is { } time)
            rows.Add(("Time", time));

        if (Charge(battery) is { } charge)
            rows.Add(("Charge", charge));

        if (Health(battery) is { } health)
            rows.Add(("Health", health));

        if (battery.CycleCount is { } cycles)
            rows.Add(("Cycles", cycles.ToString(Invariant)));

        if (battery.VoltageMillivolts is { } millivolts)
            rows.Add(("Voltage", $"{(millivolts / 1000.0).ToString("0.00", Invariant)} V"));

        if (Identity(battery) is { } identity)
            rows.Add(("Battery", identity));

        return rows;
    }

    /// <summary>Full-charge capacity as a percentage of the capacity when new, or null without both.</summary>
    public static int? HealthPercent(BatteryDetails battery) =>
        battery is { FullChargeMilliwattHours: { } full, DesignMilliwattHours: { } design } && design > 0
            ? (int)Math.Round(100.0 * full / design)
            : null;

    internal static string Status(BatteryDetails battery) => battery switch
    {
        { Charging: true } => "Charging",
        { Discharging: true } => "On battery",
        { PluggedIn: true } => "Plugged in, not charging",
        _ => "On battery"
    };

    // Only a rate that matches what the battery is doing is shown.
    internal static string? Power(BatteryDetails battery) => battery.RateMilliwatts switch
    {
        > 0 and var rate when battery.Charging => $"Charging at {Watts(rate)}",
        < 0 and var rate when battery.Discharging => $"Using {Watts(-rate)}",
        _ => null
    };

    // On battery: Windows' own estimate, or the charge left over the rate.
    // Charging: the missing charge over the rate.
    internal static string? Time(BatteryDetails battery)
    {
        if (battery.Discharging)
        {
            var seconds = battery.EstimatedSecondsLeft ??
                (battery is { RemainingMilliwattHours: { } left, RateMilliwatts: < 0 and var rate }
                    ? (int)(left * 3600.0 / -rate)
                    : null);

            return seconds is { } s ? $"About {Duration(s)} left" : null;
        }

        if (battery is { Charging: true, RemainingMilliwattHours: { } now, FullChargeMilliwattHours: { } full, RateMilliwatts: > 0 and var charging } && full > now)
            return $"About {Duration((int)((full - now) * 3600.0 / charging))} to full";

        return null;
    }

    internal static string? Charge(BatteryDetails battery)
    {
        if (battery.RemainingMilliwattHours is not { } now)
            return null;

        if (battery.FullChargeMilliwattHours is not { } full || full <= 0)
            return WattHours(now);

        return $"{WattHours(now)} of {WattHours(full)} ({(int)Math.Round(100.0 * now / full)}%)";
    }

    internal static string? Health(BatteryDetails battery) =>
        HealthPercent(battery) is { } percent
            ? $"{percent}% ({WattHours(battery.FullChargeMilliwattHours!.Value)} of {WattHours(battery.DesignMilliwattHours!.Value)} when new)"
            : null;

    internal static string? Identity(BatteryDetails battery)
    {
        var name = string.Join(" ", new[] { battery.Manufacturer, battery.Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var chemistry = ChemistryName(battery.Chemistry);

        return (name.Length, chemistry) switch
        {
            (0, null) => null,
            (0, _) => chemistry,
            (_, null) => name,
            _ => $"{name} · {chemistry}"
        };
    }

    // The driver gives four letters, sometimes cut short ("Li-I" for "Li-Ion").
    internal static string? ChemistryName(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "LION" or "LI-I" or "LI-ION" or "LIION" => "Lithium-ion",
        "LIP" or "LIPO" or "LI-P" => "Lithium polymer",
        "NIMH" => "Nickel-metal hydride",
        "NICD" => "Nickel-cadmium",
        "PBAC" => "Lead-acid",
        _ => code
    };

    internal static string Duration(int seconds)
    {
        var minutes = Math.Max(1, (int)Math.Round(seconds / 60.0));
        var hours = minutes / 60;

        return hours > 0 ? $"{hours} h {minutes % 60} min" : $"{minutes} min";
    }

    private static string Watts(int milliwatts) =>
        $"{(milliwatts / 1000.0).ToString("0.0", Invariant)} W";

    private static string WattHours(int milliwattHours) =>
        $"{(milliwattHours / 1000.0).ToString("0.0", Invariant)} Wh";
}
