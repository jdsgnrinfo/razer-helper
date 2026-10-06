using System.Globalization;
using RazerHelper.Core.Localization;
using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>When the keyboard light goes off by itself: with the screen, after a while idle, on low battery.</summary>
internal sealed record LightsOffChoices(bool WithScreen, bool WhenIdle, int IdleMinutes, bool OnLowBattery, int BatteryPercent);

/// <summary>
/// The keyboard and logo lighting, and when the lighting goes off by itself:
/// one line each, its choice of time or charge beside the words and its
/// switch at the right.
/// </summary>
internal sealed class LightingPage : PageView
{
    /// <summary>The idle times offered, in minutes.</summary>
    internal static readonly int[] IdleMinutes = [1, 2, 5, 10, 15, 30];

    /// <summary>The battery charges offered, in percent.</summary>
    internal static readonly int[] BatteryPercents = [10, 15, 20, 25, 30, 40, 50];

    private static int RowHeight => S(44);

    private readonly ToggleSwitch _withScreen;
    private readonly ToggleSwitch _whenIdle;
    private readonly ToggleSwitch _onLowBattery;
    private readonly DropdownButton _idleMinutes;
    private readonly DropdownButton _batteryPercent;
    private bool _showing;

    public LightingPage(LightingSection lighting, LightsOffChoices lightsOff)
        : base("Lighting")
    {
        var stack = new SectionStack();
        stack.AddSection(lighting, () => LightingSection.ContentHeight);
        AddWide(stack);

        var divider = CreateDivider();
        divider.Margin = new Padding(0, S(22), 0, S(6));
        Add(divider);

        _idleMinutes = CreateChoice(IdleMinutes.Select(minutes => L.F("{0} min", minutes)));
        _batteryPercent = CreateChoice(BatteryPercents.Select(percent => string.Format(CultureInfo.CurrentCulture, "{0}%", percent)));

        Add(CreateOptionRow("Turn off lighting when display is off", null, out _withScreen));
        Add(CreateOptionRow("Turn off lighting when idle for:", _idleMinutes, out _whenIdle));
        Add(CreateOptionRow("Turn off lighting when battery is below:", _batteryPercent, out _onLowBattery));

        Show(lightsOff);

        foreach (var toggle in new[] { _withScreen, _whenIdle, _onLowBattery })
            toggle.CheckedChanged += (_, _) => Report();

        _idleMinutes.SelectionChanged += (_, _) => Report();
        _batteryPercent.SelectionChanged += (_, _) => Report();
    }

    /// <summary>Raised with every choice whenever one changes.</summary>
    public event EventHandler<LightsOffChoices>? LightsOffChanged;

    private void Show(LightsOffChoices choices)
    {
        _showing = true;
        _withScreen.Checked = choices.WithScreen;
        _whenIdle.Checked = choices.WhenIdle;
        _onLowBattery.Checked = choices.OnLowBattery;
        _idleMinutes.Select(Nearest(IdleMinutes, choices.IdleMinutes));
        _batteryPercent.Select(Nearest(BatteryPercents, choices.BatteryPercent));
        _showing = false;
    }

    private void Report()
    {
        if (_showing)
            return;

        LightsOffChanged?.Invoke(this, new LightsOffChoices(
            _withScreen.Checked,
            _whenIdle.Checked,
            IdleMinutes[Math.Max(0, _idleMinutes.SelectedIndex)],
            _onLowBattery.Checked,
            BatteryPercents[Math.Max(0, _batteryPercent.SelectedIndex)]));
    }

    // The offered value closest to a saved one (a settings file edited by hand may hold any).
    private static int Nearest(int[] offered, int value) =>
        Array.IndexOf(offered, offered.MinBy(each => Math.Abs(each - value)));

    private static DropdownButton CreateChoice(IEnumerable<string> items) => new([.. items])
    {
        Anchor = AnchorStyles.Left,
        Font = SemiBoldTitleFont(15),
        Margin = new Padding(S(12), 0, 0, 0),
        Size = new Size(S(112), S(36))
    };

    // The words, the choice beside them when there is one, and the switch at the right.
    private static TableLayoutPanel CreateOptionRow(string text, Control? choice, out ToggleSwitch toggle)
    {
        var created = toggle = new ToggleSwitch
        {
            AccessibleName = L.T(text),
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty
        };

        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1,
            Size = new Size(ContentWidth, RowHeight)
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var words = new Label
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Font = DesignFont(15),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            Text = L.T(text)
        };

        // Clicking the words flips the switch, as on the other settings.
        words.Click += (_, _) => created.Checked = !created.Checked;

        row.Controls.Add(words, 0, 0);

        if (choice is not null)
            row.Controls.Add(choice, 1, 0);

        row.Controls.Add(created, 2, 0);
        return row;
    }
}
