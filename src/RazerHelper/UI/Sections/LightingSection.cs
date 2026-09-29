using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Keyboard backlight and lid logo, each as a small stack: its name, then its
/// effect drop-down (and the color list beside it where one applies), then
/// its brightness slider. Stacked rather than side by side so the section
/// fits a narrow window. Always available, on battery or plugged in (the
/// laptop has no power-source rule for lighting), and never part of the power
/// profiles.
/// </summary>
/// <remarks>
/// The laptop is the source of truth. The controls show what it reports when
/// the popup opens and again after every change, so if a change is refused the
/// display goes back to what is really lit instead of showing a wrong choice.
/// </remarks>
internal sealed class LightingSection : SectionPanel
{
    private static int LineGap => S(6);

    /// <summary>The header and the two stacks, the logo's a little below the keyboard's.</summary>
    public static int ContentHeight => SectionHeaderHeight + 2 * Line.Height + LineGap;

    // The effects offered, in the list's order. Wave only where the keyboard
    // has zones to move across (see RazerLaptopModel.HasWaveEffect).
    private readonly KeyboardEffect[] _keyboardEffects;

    private static readonly LogoMode[] LogoModes = [LogoMode.Off, LogoMode.On, LogoMode.Breathing];

    private readonly LightingService _lightingService;
    private readonly Line _keyboard;
    private readonly Line _logo;

    // The colors offered in the color list, in its order.
    private static readonly (string Name, RgbColor Color)[] PresetColors =
    [
        ("White", RgbColor.White),
        ("Razer green", new RgbColor(0x44, 0xD6, 0x2C)),
        ("Red", new RgbColor(0xFF, 0x00, 0x00)),
        ("Orange", new RgbColor(0xFF, 0x80, 0x00)),
        ("Yellow", new RgbColor(0xFF, 0xFF, 0x00)),
        ("Cyan", new RgbColor(0x00, 0xFF, 0xFF)),
        ("Blue", new RgbColor(0x00, 0x00, 0xFF)),
        ("Purple", new RgbColor(0x80, 0x00, 0xFF)),
        ("Pink", new RgbColor(0xFF, 0x00, 0x80))
    ];

    // Only on models that show a chosen color, and only visible while the
    // keyboard is on Static or Breathing: the color list, in the same dark
    // drop-down style, beside the keyboard's effect.
    private readonly DropdownButton? _colorDropdown;
    private RgbColor _color = RgbColor.White;

    // The keyboard effect the laptop last reported, which a new color keeps.
    private KeyboardEffect? _keyboardEffect;

    private bool _busy;

    public LightingSection(LightingService lightingService, bool offersColor = false, bool offersWave = true)
    {
        _lightingService = lightingService;
        _keyboardEffects = offersWave
            ? [KeyboardEffect.Off, KeyboardEffect.StaticGreen, KeyboardEffect.Spectrum, KeyboardEffect.Wave, KeyboardEffect.Breathing]
            : [KeyboardEffect.Off, KeyboardEffect.StaticGreen, KeyboardEffect.Spectrum, KeyboardEffect.Breathing];

        if (offersColor)
        {
            _colorDropdown = new DropdownButton(
                [.. PresetColors.Select(preset => L.T(preset.Name))],
                swatches: [.. PresetColors.Select(preset => (Color?)ToColor(preset.Color))])
            {
                Visible = false
            };

            _colorDropdown.SelectionChanged += async (_, _) => await PickColorAsync(_colorDropdown.SelectedIndex);
        }

        _keyboard = new Line("Keyboard", _keyboardEffects.Select(effect => Describe(effect, offersColor)), _colorDropdown);
        _logo = new Line("Logo", LogoModes.Select(Describe));

        _keyboard.Effect.SelectionChanged += async (_, _) =>
        {
            var effect = _keyboardEffects[_keyboard.Effect.SelectedIndex];

            // Where a color can be shown, Static and Breathing use that color.
            await ApplyAsync(
                offersColor && IsColored(effect)
                    ? () => SetColoredEffectAsync(effect, _color)
                    : () => _lightingService.SetKeyboardEffectAsync(effect),
                L.T("Could not change the keyboard lighting."));
        };

        _keyboard.Brightness.Committed += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetKeyboardBrightnessAsync(_keyboard.Brightness.Value),
                L.T("Could not change the keyboard brightness."));

        _logo.Effect.SelectionChanged += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetLogoAsync(LogoModes[_logo.Effect.SelectedIndex]),
                L.T("Could not change the logo lighting."));
        _logo.Brightness.Committed += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetLogoBrightnessAsync(_logo.Brightness.Value),
                L.T("Could not change the logo brightness."));

        var lines = new TableLayoutPanel
        {
            BackColor = CardColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 4
        };

        lines.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        lines.RowStyles.Add(new RowStyle(SizeType.Absolute, Line.Height));
        lines.RowStyles.Add(new RowStyle(SizeType.Absolute, LineGap));
        lines.RowStyles.Add(new RowStyle(SizeType.Absolute, Line.Height));
        lines.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Takes the spare height, so the two stacks keep their size.
        lines.Controls.Add(_keyboard.Panel, 0, 0);
        lines.Controls.Add(_logo.Panel, 0, 2);

        // Dock order: the header docks first, and the stacks fill what is left.
        Controls.Add(lines);
        Controls.Add(CreateSectionHeader("Lighting", string.Empty, Glyph.Lighting));
    }

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    private async Task PickColorAsync(int index)
    {
        if (index < 0)
            return;

        var color = PresetColors[index].Color;

        // The list is only shown on a colored effect; the new color keeps it.
        var effect = _keyboardEffect is { } shown && IsColored(shown) ? shown : KeyboardEffect.StaticGreen;

        _color = color;
        await ApplyAsync(
            () => SetColoredEffectAsync(effect, color),
            L.T("Could not change the keyboard color."));
    }

    // The effects that show the chosen color, on models that offer one.
    private static bool IsColored(KeyboardEffect effect) =>
        effect is KeyboardEffect.StaticGreen or KeyboardEffect.Breathing;

    private Task SetColoredEffectAsync(KeyboardEffect effect, RgbColor color) =>
        effect == KeyboardEffect.Breathing
            ? _lightingService.SetKeyboardBreathingAsync(color)
            : _lightingService.SetKeyboardColorAsync(color);

    // Selects the matching preset. A color set some other way that is not in
    // the list shows no selection rather than a wrong one.
    private void ShowColor(RgbColor color) =>
        _colorDropdown?.Select(Array.FindIndex(PresetColors, candidate => candidate.Color == color));

    // The color list only means something while the keyboard is on Static or
    // Breathing. Its half of the row stays reserved, so the effect list beside
    // it never changes size.
    private void SetColorListShown(bool shown)
    {
        if (_colorDropdown is not null)
            _colorDropdown.Visible = shown;
    }

    private static Color ToColor(RgbColor color) => Color.FromArgb(color.Red, color.Green, color.Blue);

    /// <summary>Shows what the laptop's lighting is actually set to.</summary>
    public async Task RefreshAsync()
    {
        if (_busy)
            return;

        _busy = true;

        var state = await ReadStateOrUnknownAsync(L.T("Could not read the lighting state.")).ConfigureAwait(false);

        await PostToUiAsync(() =>
        {
            ShowState(state);
            EndBusy();
        });
    }

    // Runs one change, then shows what the laptop really did, whatever happened.
    private async Task ApplyAsync(Func<Task> change, string failureMessage)
    {
        if (_busy)
        {
            // Something else is talking to the laptop: put the display back to the truth.
            _ = RefreshAsync();
            return;
        }

        _busy = true;
        SetControlsEnabled(false);

        Exception? failure = null;

        try
        {
            await change().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        var state = await ReadStateOrUnknownAsync(L.T("Could not read the lighting state after a change.")).ConfigureAwait(false);

        await PostToUiAsync(() =>
        {
            ShowState(state);

            if (failure is not null)
            {
                AppLog.Error(failureMessage, failure);
                StatusChanged?.Invoke(this, new SectionStatus(failureMessage, IsError: true));
            }

            EndBusy();
        });
    }

    // Where a color can be chosen, the static effect shows that color, not green.
    private static string Describe(KeyboardEffect effect, bool offersColor) =>
        L.T(effect == KeyboardEffect.StaticGreen ? (offersColor ? "Static" : "Static green") : effect.ToString());

    private static string Describe(LogoMode mode) => L.T(mode == LogoMode.On ? "On" : mode.ToString());

    // A failed read is logged and shown as "unknown", never as an old value.
    private async Task<LightingState> ReadStateOrUnknownAsync(string logMessage)
    {
        try
        {
            return await _lightingService.ReadStateAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AppLog.Error(logMessage, exception);
            return LightingState.Unknown;
        }
    }

    private void EndBusy()
    {
        _busy = false;
        SetControlsEnabled(true);
    }

    private void SetControlsEnabled(bool enabled)
    {
        _keyboard.Enabled = enabled;
        _logo.Enabled = enabled;

        if (_colorDropdown is not null)
            _colorDropdown.Enabled = enabled;
    }

    private void ShowState(LightingState state)
    {
        // The list shows the color the laptop reports, so the next Static
        // (or the picker) starts from what is really lit.
        if (state.KeyboardColor is { } color)
        {
            _color = color;
            ShowColor(color);
        }

        _keyboardEffect = state.Keyboard;
        SetColorListShown(state.Keyboard is { } shown && IsColored(shown));

        _keyboard.Show(state.Keyboard is { } effect ? Array.IndexOf(_keyboardEffects, effect) : -1, state.KeyboardBrightness);
        _logo.Show(state.Logo is { } mode ? Array.IndexOf(LogoModes, mode) : -1, state.LogoBrightness);
    }

    /// <summary>
    /// One light as a stack of three rows: its name; its effect drop-down, with
    /// an optional second drop-down (the color list) beside it; and its
    /// brightness slider with the percentage.
    /// </summary>
    private sealed class Line
    {
        private static int NameHeight => S(16 + 6);
        private static int ChoiceHeight => S(36 + 6);
        private static int SliderHeight => S(20);

        /// <summary>The whole stack's height.</summary>
        public static int Height => NameHeight + ChoiceHeight + SliderHeight;

        private readonly Label _percent;

        public Line(string name, IEnumerable<string> effects, DropdownButton? companion = null)
        {
            Effect = new DropdownButton(effects.ToArray())
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, companion is null ? 0 : ButtonGap / 2, S(6))
            };

            Brightness = new ThemedSlider(LightingBrightness.MinimumPercent, LightingBrightness.MaximumPercent, 5)
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty
            };

            _percent = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = DesignFont(12, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = Padding.Empty,
                Text = "--",
                TextAlign = ContentAlignment.MiddleRight
            };

            // The percentage follows the slider while it is dragged, before anything is sent.
            Brightness.ValueChanged += (_, _) => _percent.Text = $"{Brightness.Value} %";

            // The effect and its companion share the row half and half, so
            // the effect list keeps its width whether the companion shows.
            var choices = new TableLayoutPanel
            {
                BackColor = CardColor,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 1
            };

            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            choices.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            choices.Controls.Add(Effect, 0, 0);

            if (companion is not null)
            {
                companion.Dock = DockStyle.Fill;
                companion.Margin = new Padding(ButtonGap / 2, 0, 0, S(6));
                choices.Controls.Add(companion, 1, 0);
            }
            else
            {
                // Nothing beside it (the logo): the list takes the whole row.
                choices.SetColumnSpan(Effect, 2);
            }

            var slider = new TableLayoutPanel
            {
                BackColor = CardColor,
                ColumnCount = 2,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 1
            };

            slider.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            slider.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(56)));
            slider.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            slider.Controls.Add(Brightness, 0, 0);
            slider.Controls.Add(_percent, 1, 0);

            Panel = new TableLayoutPanel
            {
                BackColor = CardColor,
                ColumnCount = 1,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 3
            };

            Panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Panel.RowStyles.Add(new RowStyle(SizeType.Absolute, NameHeight));
            Panel.RowStyles.Add(new RowStyle(SizeType.Absolute, ChoiceHeight));
            Panel.RowStyles.Add(new RowStyle(SizeType.Absolute, SliderHeight));
            Panel.Controls.Add(new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = DesignFont(11, FontStyle.Bold),
                ForeColor = Color.White,
                Margin = new Padding(0, 0, 0, S(6)),
                Text = L.T(name),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            Panel.Controls.Add(choices, 0, 1);
            Panel.Controls.Add(slider, 0, 2);
        }

        /// <summary>The whole stack, to place in the section.</summary>
        public TableLayoutPanel Panel { get; }

        public DropdownButton Effect { get; }

        public ThemedSlider Brightness { get; }

        public bool Enabled
        {
            set
            {
                Effect.Enabled = value;
                Brightness.Enabled = value;
                // The percentage greys out with the slider.
                _percent.ForeColor = value ? Color.White : SubtleTextColor;
            }
        }

        /// <summary>Shows what the laptop reports: an effect (or -1 for none) and a brightness (or null to leave it).</summary>
        public void Show(int effectIndex, int? brightness)
        {
            Effect.Select(effectIndex);

            if (brightness is { } percent)
                Brightness.Value = percent;
        }

    }
}
