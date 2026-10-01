using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The Battery details window, opened from the Battery section: the header
/// "55% (Charging)", then cards for power in or out, time left or to full,
/// charge and health (each with a bar), voltage and the battery itself.
/// </summary>
internal sealed class BatteryDetailsForm(Func<BatteryDetails?> read) : DetailsWindow("Battery", semiBoldValues: true)
{
    private static readonly Color HealthFair = Color.FromArgb(230, 170, 40);

    protected override (string Header, IReadOnlyList<BentoCardSpec> Cards) ReadCards()
    {
        if (read() is not { } battery)
            return ($"--% ({L.T("Unavailable")})", [new BentoCardSpec("Battery", L.T("No information"), L.T("Windows did not report the battery"), Wide: true)]);

        var percent = BatteryDetailsText.ChargePercent(battery);
        var header = $"{(percent is { } p ? $"{p}%" : "--%")} ({BatteryDetailsText.HeaderStatus(battery)})";

        return (header, CardsFor(battery));
    }

    private static List<BentoCardSpec> CardsFor(BatteryDetails battery)
    {
        var cards = new List<BentoCardSpec>();

        if (BatteryDetailsText.PowerParts(battery) is var (watts, direction))
            cards.Add(new BentoCardSpec("Power", watts, direction));

        if (BatteryDetailsText.TimeParts(battery) is var (time, towards))
            cards.Add(new BentoCardSpec("Time", time, towards));

        if (BatteryDetailsText.ChargeParts(battery) is var (stored, outOf))
        {
            var percent = BatteryDetailsText.ChargePercent(battery);
            cards.Add(new BentoCardSpec("Charge", outOf is null ? stored : $"{stored} {outOf}", null, Bar: percent / 100.0, BarColor: RazerGreen));
        }

        if (BatteryDetailsText.HealthPercent(battery) is { } health)
        {
            cards.Add(new BentoCardSpec(
                "Health",
                $"{health}%",
                BatteryDetailsText.HealthCaption(battery),
                Bar: Math.Min(1.0, health / 100.0),
                BarColor: health >= 80 ? RazerGreen : health >= 60 ? HealthFair : Color.IndianRed));
        }

        if (BatteryDetailsText.Voltage(battery) is { } voltage)
            cards.Add(new BentoCardSpec("Voltage", voltage, null));

        var name = string.Join(" ", new[] { battery.Manufacturer, battery.Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var chemistry = BatteryDetailsText.ChemistryName(battery.Chemistry);

        if (name.Length > 0 || chemistry is not null)
            cards.Add(new BentoCardSpec("Battery", (name.Length, chemistry) switch
            {
                (> 0, not null) => $"{name} ({chemistry})",
                (> 0, null) => name,
                _ => chemistry!
            }, null));

        if (battery.CycleCount is { } cycles)
            cards.Add(new BentoCardSpec("Cycles", cycles.ToString(), null));

        return cards;
    }
}
