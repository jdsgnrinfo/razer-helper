using System.Globalization;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The panel's figures at the top of Display, compact, three to a row: its
/// model and maker, its size, its native resolution, its fastest refresh
/// rate, the bits per color it is sent and whether it can show HDR. Read
/// each time the page comes on screen (they change only with the screen or
/// its settings), not every refresh.
/// </summary>
internal sealed class DisplayInfoView(Func<DisplayPanel?> read, Func<IReadOnlyList<int>> refreshRates)
    : DetailsView("Display", semiBoldValues: true, compact: true)
{
    private DisplayPanel? _panel;
    private int? _fastest;
    private bool _read;

    public override void Start()
    {
        try
        {
            _panel = read();
            _fastest = refreshRates() is { Count: > 0 } rates ? rates.Max() : null;
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the panel's details.", exception);
            _panel = null;
        }

        _read = true;
        base.Start();
    }

    protected override IReadOnlyList<BentoCardSpec> ReadCards()
    {
        if (!_read)
            return [];

        return CardsFor(_panel, _fastest, CultureInfo.CurrentCulture) is { Count: > 0 } cards
            ? cards
            : [new BentoCardSpec("Panel", L.T("No information"), L.T("Windows did not report the panel"), DetailBeside: true)];
    }

    private static List<BentoCardSpec> CardsFor(DisplayPanel? panel, int? fastest, CultureInfo culture)
    {
        var cards = new List<BentoCardSpec>();
        var edid = panel?.Edid;

        if (edid is not null)
        {
            // "Panel · BOE" over its part number; with no part number, the maker is the figure.
            var maker = DisplayPanelText.MakerName(edid.MakerCode);
            cards.Add(edid.Model is { } model
                ? new BentoCardSpec($"{L.T("Panel")} · {maker}", model, null)
                : new BentoCardSpec("Panel", maker, null));

            if (edid.DiagonalInches is { } inches)
            {
                cards.Add(new BentoCardSpec(
                    "Size",
                    DisplayPanelText.Diagonal(inches, culture),
                    DisplayPanelText.Area(edid.WidthMillimeters!.Value, edid.HeightMillimeters!.Value, culture),
                    DetailBeside: true));
            }

            if (edid is { NativeWidth: { } width, NativeHeight: { } height })
                cards.Add(new BentoCardSpec("Resolution", DisplayPanelText.Resolution(width, height), null));
        }

        if (fastest is { } hz)
            cards.Add(new BentoCardSpec("Max refresh", $"{hz} Hz", null));

        if (panel?.BitsPerColor is { } bits)
            cards.Add(new BentoCardSpec("Color depth", L.F("{0} bits", bits), null));

        if (panel?.HdrSupported is { } hdr)
        {
            cards.Add(new BentoCardSpec(
                "HDR",
                hdr ? L.T("Supported") : L.T("Not supported"),
                hdr && panel.HdrOn == true ? L.T("On") : null,
                DetailBeside: true));
        }

        return cards;
    }
}
