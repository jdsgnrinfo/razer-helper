using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The battery's figures at the top of Power profiles, compact, three to a
/// row, so the profiles under them fit without scrolling: charge and health
/// (each with a bar), power in or out and the time left or to full while
/// there are any, the voltage and the battery itself, with its chemistry
/// and cycles in its caption.
/// </summary>
internal sealed class BatteryDetailsView(Func<BatteryDetails?> read) : DetailsView("Battery", semiBoldValues: true, compact: true)
{
    private static readonly Color HealthFair = Color.FromArgb(230, 170, 40);

    protected override IReadOnlyList<BentoCardSpec> ReadCards() =>
        read() is { } battery
            ? CardsFor(battery)
            : [new BentoCardSpec("Battery", L.T("No information"), L.T("Windows did not report the battery"), DetailBeside: true)];

    private static List<BentoCardSpec> CardsFor(BatteryDetails battery)
    {
        var cards = new List<BentoCardSpec>();

        // Each note beside its figure, so no card takes a second line.
        if (BatteryDetailsText.ChargePercent(battery) is { } percent)
            cards.Add(new BentoCardSpec("Charge", $"{percent}%", BatteryDetailsText.Stored(battery), Bar: percent / 100.0, BarColor: RazerGreen, DetailBeside: true));

        if (BatteryDetailsText.HealthPercent(battery) is { } health)
        {
            cards.Add(new BentoCardSpec(
                "Health",
                $"{health}%",
                BatteryDetailsText.HealthShort(battery),
                Bar: Math.Min(1.0, health / 100.0),
                BarColor: health >= 80 ? RazerGreen : health >= 60 ? HealthFair : Color.IndianRed,
                DetailBeside: true));
        }

        if (BatteryDetailsText.PowerParts(battery) is var (watts, direction))
            cards.Add(new BentoCardSpec("Power", watts, direction, DetailBeside: true));

        if (BatteryDetailsText.TimeParts(battery) is var (time, towards))
            cards.Add(new BentoCardSpec("Time", time, towards, DetailBeside: true));

        if (BatteryDetailsText.Voltage(battery) is { } voltage)
            cards.Add(new BentoCardSpec("Voltage", voltage, null));

        var name = string.Join(" ", new[] { battery.Manufacturer, battery.Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var chemistry = BatteryDetailsText.ChemistryName(battery.Chemistry);

        if (name.Length > 0 || chemistry is not null)
        {
            // "Battery · Lithium ion · 120 cycles" over the name.
            var caption = string.Join(" · ", new[]
            {
                L.T("Battery"),
                name.Length > 0 ? chemistry : null,
                battery.CycleCount is { } cycles ? L.F("{0} cycles", cycles) : null
            }.Where(part => part is not null));

            cards.Add(new BentoCardSpec(caption, name.Length > 0 ? name : chemistry!, null));
        }

        return cards;
    }
}
