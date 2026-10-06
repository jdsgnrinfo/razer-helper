using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// Keyboard backlight and lid logo, one under the other at the left, each
/// its name, its effects as pictures to pick from and its brightness slider;
/// at the right, beside the keyboard's, a preview: the effect's name with
/// Reset, the colors to choose from (where the laptop shows a chosen color)
/// and a small keyboard playing the effect. Always available, on battery or
/// plugged in (the laptop has no power-source rule for lighting), and never
/// part of the power profiles.
/// </summary>
/// <remarks>
/// The laptop is the source of truth. The controls show what it reports when
/// the popup opens and again after every change, so if a change is refused the
/// display goes back to what is really lit instead of showing a wrong choice.
/// </remarks>
internal sealed class LightingSection : SectionPanel
{
    private static int PreviewWidth => S(230);
    private static int ColumnGap => S(20);

    // The effects' column, and each effect's cell: five to a row, so the logo's three line up under the keyboard's.
    private static int EffectsWidth => Pages.PageView.ContentWidth - PreviewWidth - ColumnGap;
    private static int CellWidth => EffectsWidth / 5;

    private static int TitleHeight => S(20 + 10);
    private static int SliderHeight => S(36);
    private static int LogoGap => S(14);

    /// <summary>The keyboard's and the logo's stacks, one under the other.</summary>
    public static int ContentHeight => 2 * (TitleHeight + EffectPicker.PickerHeight + SliderHeight) + LogoGap;

    // The effects offered, in the pictures' order. Wave only where the
    // keyboard has zones to move across (see RazerLaptopModel.HasWaveEffect).
    private readonly KeyboardEffect[] _keyboardEffects;

    private static readonly LogoMode[] LogoModes = [LogoMode.On, LogoMode.Breathing, LogoMode.Off];

    private readonly LightingService _lightingService;
    private readonly Light _keyboard;
    private readonly Light _logo;
    private readonly LightingPreview _preview;
    private readonly bool _offersColor;

    // The colors offered, in their order: Razer green first, as the app's own.
    private static readonly (string Name, RgbColor Color)[] PresetColors =
    [
        ("Razer green", new RgbColor(0x44, 0xD6, 0x2C)),
        ("White", RgbColor.White),
        ("Red", new RgbColor(0xFF, 0x00, 0x00)),
        ("Orange", new RgbColor(0xFF, 0x80, 0x00)),
        ("Yellow", new RgbColor(0xFF, 0xFF, 0x00)),
        ("Cyan", new RgbColor(0x00, 0xFF, 0xFF)),
        ("Blue", new RgbColor(0x00, 0x00, 0xFF)),
        ("Purple", new RgbColor(0x80, 0x00, 0xFF)),
        ("Pink", new RgbColor(0xFF, 0x00, 0x80))
    ];

    private RgbColor _color = PresetColors[0].Color;

    // The keyboard effect the laptop last reported, which a new color keeps.
    private KeyboardEffect? _keyboardEffect;

    private bool _busy;

    // A change asked for while another was on its way to the laptop: the
    // latest only, run as soon as the laptop is free.
    private (Func<Task> Change, string FailureMessage, bool ReadBack)? _queued;

    // While the window is open, the keyboard brightness is read every few
    // tenths of a second, so the slider follows the Fn brightness keys (the
    // laptop handles those itself and tells nobody).
    private const int BrightnessWatchMilliseconds = 300;
    private readonly System.Windows.Forms.Timer _brightnessWatch = new() { Interval = BrightnessWatchMilliseconds };
    private bool _readingBrightness;

    public LightingSection(LightingService lightingService, bool offersColor = false, bool offersWave = true)
    {
        _lightingService = lightingService;
        _offersColor = offersColor;
        _keyboardEffects = offersWave
            ? [KeyboardEffect.StaticGreen, KeyboardEffect.Breathing, KeyboardEffect.Spectrum, KeyboardEffect.Wave, KeyboardEffect.Off]
            : [KeyboardEffect.StaticGreen, KeyboardEffect.Breathing, KeyboardEffect.Spectrum, KeyboardEffect.Off];

        _keyboard = new Light("Keyboard", [.. _keyboardEffects.Select(effect => (Describe(effect, offersColor), IconFor(effect)))]);
        _logo = new Light("Logo", [.. LogoModes.Select(mode => (Describe(mode), IconFor(mode)))]);

        _preview = new LightingPreview(offersColor ? [.. PresetColors.Select(preset => ToColor(preset.Color))] : []);
        _preview.ColorPicked += async (_, index) => await PickColorAsync(index);
        _preview.ResetRequested += async (_, _) => await ResetKeyboardAsync();

        _keyboard.Effects.SelectionChanged += async (_, _) =>
        {
            var effect = _keyboardEffects[_keyboard.Effects.PickedIndex];

            // Where a color can be shown, Static and Breathing use that color.
            await ApplyAsync(
                offersColor && IsColored(effect)
                    ? () => SetColoredEffectAsync(effect, _color)
                    : () => _lightingService.SetKeyboardEffectAsync(effect),
                L.T("Could not change the keyboard lighting."));
        };

        // The light follows the slider while it is dragged; letting go sends
        // the final value and shows what the laptop then reports.
        _keyboard.Brightness.ValueChanged += async (_, _) =>
        {
            ShowPreview();

            if (_keyboard.Brightness.IsBeingMoved)
                await ApplyAsync(
                    () => _lightingService.SetKeyboardBrightnessAsync(_keyboard.Brightness.Value),
                    L.T("Could not change the keyboard brightness."),
                    readBack: false);
        };
        _keyboard.Brightness.Committed += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetKeyboardBrightnessAsync(_keyboard.Brightness.Value),
                L.T("Could not change the keyboard brightness."));

        _logo.Effects.SelectionChanged += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetLogoAsync(LogoModes[_logo.Effects.PickedIndex]),
                L.T("Could not change the logo lighting."));
        _logo.Brightness.ValueChanged += async (_, _) =>
        {
            if (_logo.Brightness.IsBeingMoved)
                await ApplyAsync(
                    () => _lightingService.SetLogoBrightnessAsync(_logo.Brightness.Value),
                    L.T("Could not change the logo brightness."),
                    readBack: false);
        };
        _logo.Brightness.Committed += async (_, _) =>
            await ApplyAsync(
                () => _lightingService.SetLogoBrightnessAsync(_logo.Brightness.Value),
                L.T("Could not change the logo brightness."));

        // The keyboard's stack at the top left, the logo's under it, the preview beside the keyboard's.
        _keyboard.Place(this, 0);
        _logo.Place(this, _keyboard.Bottom + LogoGap);
        // Its height follows from its width, which sets how many colors fit in a row.
        _preview.Bounds = new Rectangle(EffectsWidth + ColumnGap, 0, PreviewWidth, 0);
        _preview.Height = _preview.PreferredHeight;
        Controls.Add(_preview);

        ShowPreview();
        _brightnessWatch.Tick += async (_, _) => await ReadKeyboardBrightnessAsync();
    }

    /// <summary>Starts following the keyboard brightness, for while the window is open.</summary>
    public void StartWatchingBrightness() => _brightnessWatch.Start();

    public void StopWatchingBrightness() => _brightnessWatch.Stop();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _brightnessWatch.Dispose();

        base.Dispose(disposing);
    }

    // One quick read; the slider glides to it unless the user is moving it or a
    // change is on its way to the laptop.
    private async Task ReadKeyboardBrightnessAsync()
    {
        if (_busy || _readingBrightness || _keyboard.Brightness.IsBeingMoved)
            return;

        _readingBrightness = true;

        try
        {
            var percent = await _lightingService.ReadKeyboardBrightnessAsync();

            if (!_busy && _brightnessWatch.Enabled)
                _keyboard.Brightness.GlideTo(percent);
        }
        catch
        {
            // A missed reading is fine: the next one is a moment away, and
            // RefreshAsync logs real trouble.
        }
        finally
        {
            _readingBrightness = false;
        }
    }

    /// <summary>Raised with a user-facing message about the last operation.</summary>
    public event EventHandler<SectionStatus>? StatusChanged;

    private async Task PickColorAsync(int index)
    {
        if (index < 0)
            return;

        var color = PresetColors[index].Color;

        // A colored effect keeps going in the new color; any other becomes Static in it.
        var effect = _keyboardEffect is { } shown && IsColored(shown) ? shown : KeyboardEffect.StaticGreen;

        _color = color;
        await ApplyAsync(
            () => SetColoredEffectAsync(effect, color),
            L.T("Could not change the keyboard color."));
    }

    // Reset: the keyboard back to Static, in Razer green where a color can be chosen.
    private Task ResetKeyboardAsync()
    {
        _color = PresetColors[0].Color;

        return ApplyAsync(
            _offersColor
                ? () => SetColoredEffectAsync(KeyboardEffect.StaticGreen, _color)
                : () => _lightingService.SetKeyboardEffectAsync(KeyboardEffect.StaticGreen),
            L.T("Could not change the keyboard lighting."));
    }

    // The effects that show the chosen color, on models that offer one.
    private static bool IsColored(KeyboardEffect effect) =>
        effect is KeyboardEffect.StaticGreen or KeyboardEffect.Breathing;

    private Task SetColoredEffectAsync(KeyboardEffect effect, RgbColor color) =>
        effect == KeyboardEffect.Breathing
            ? _lightingService.SetKeyboardBreathingAsync(color)
            : _lightingService.SetKeyboardColorAsync(color);

    // The preview, as the keyboard is: its effect, its color (green where none
    // can be chosen) and the brightness on the slider.
    private void ShowPreview()
    {
        var effect = _keyboardEffect switch
        {
            KeyboardEffect.Breathing => PreviewEffect.Breathing,
            KeyboardEffect.Spectrum => PreviewEffect.Spectrum,
            KeyboardEffect.Wave => PreviewEffect.Wave,
            KeyboardEffect.Off => PreviewEffect.Off,
            _ => PreviewEffect.Static
        };

        var color = _offersColor ? ToColor(_color) : RazerGreen;

        // A color set some other way that is not offered shows no choice rather than a wrong one.
        var chosen = _offersColor ? Array.FindIndex(PresetColors, candidate => candidate.Color == _color) : -1;

        _preview.Show(effect, _keyboard.Effects.SelectedName, chosen, color, _keyboard.Brightness.Value);
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

    // Runs one change, then shows what the laptop really did, whatever
    // happened. Nothing is greyed meanwhile: a change asked for in the
    // meantime waits its turn (the latest only) and runs straight after.
    // readBack false is for the steps of a drag, which only send: reading
    // the state back after each would slow the light down, and the release
    // reads it once at the end.
    private async Task ApplyAsync(Func<Task> change, string failureMessage, bool readBack = true)
    {
        if (_busy)
        {
            // A drag step never pushes out a change that reads back (an effect, or the release).
            if (readBack || _queued is not { ReadBack: true })
                _queued = (change, failureMessage, readBack);

            return;
        }

        _busy = true;

        Exception? failure = null;

        try
        {
            await change().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        var state = readBack || failure is not null
            ? await ReadStateOrUnknownAsync(L.T("Could not read the lighting state after a change.")).ConfigureAwait(false)
            : null;

        await PostToUiAsync(() =>
        {
            if (state is not null)
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

    // The logo lit steadily is Static, as the keyboard's.
    private static string Describe(LogoMode mode) => L.T(mode == LogoMode.On ? "Static" : mode.ToString());

    private static EffectIcon IconFor(KeyboardEffect effect) => effect switch
    {
        KeyboardEffect.Breathing => EffectIcon.Breathing,
        KeyboardEffect.Spectrum => EffectIcon.Spectrum,
        KeyboardEffect.Wave => EffectIcon.Wave,
        KeyboardEffect.Off => EffectIcon.Off,
        _ => EffectIcon.Static
    };

    private static EffectIcon IconFor(LogoMode mode) => mode switch
    {
        LogoMode.Breathing => EffectIcon.Breathing,
        LogoMode.Off => EffectIcon.Off,
        _ => EffectIcon.Static
    };

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

        if (_queued is { } next)
        {
            _queued = null;
            _ = ApplyAsync(next.Change, next.FailureMessage, next.ReadBack);
        }
    }

    private void ShowState(LightingState state)
    {
        // The preview shows the color the laptop reports, so the next Static
        // (or a color picked) starts from what is really lit.
        if (state.KeyboardColor is { } color)
            _color = color;

        _keyboardEffect = state.Keyboard;

        _keyboard.Show(state.Keyboard is { } effect ? Array.IndexOf(_keyboardEffects, effect) : -1, state.KeyboardBrightness);
        _logo.Show(state.Logo is { } mode ? Array.IndexOf(LogoModes, mode) : -1, state.LogoBrightness);
        ShowPreview();
    }

    /// <summary>One light as a stack: its name, its effects, and its brightness slider with the percentage.</summary>
    private sealed class Light
    {
        private readonly Label _title;
        private readonly Label _percent;

        public Light(string name, IReadOnlyList<(string Name, EffectIcon Icon)> effects)
        {
            _title = CreateSectionLabel(name);
            _title.Dock = DockStyle.None;

            Effects = new EffectPicker(effects, CellWidth) { AccessibleName = L.T(name) };

            Brightness = new ThemedSlider(LightingBrightness.MinimumPercent, LightingBrightness.MaximumPercent, 1)
            {
                AccessibleName = L.F("{0} brightness", L.T(name)),
                Format = value => $"{value} %"
            };

            _percent = new RollingLabel
            {
                AutoSize = false,
                Font = SemiBoldFont(14),
                ForeColor = Color.White,
                Text = "--",
                TextAlign = ContentAlignment.MiddleRight
            };

            // The percentage follows the slider while it is dragged, before anything is sent.
            Brightness.ValueChanged += (_, _) => _percent.Text = $"{Brightness.Value} %";
        }

        public EffectPicker Effects { get; }

        public ThemedSlider Brightness { get; }

        /// <summary>Where the stack ends.</summary>
        public int Bottom { get; private set; }

        /// <summary>Puts the stack in <paramref name="parent"/>, its top at <paramref name="top"/>, as wide as the effects.</summary>
        public void Place(Control parent, int top)
        {
            _title.Location = new Point(0, top);
            Effects.Location = new Point(0, top + TitleHeight);

            var sliderTop = Effects.Bottom;
            var percentWidth = S(50);
            Brightness.Bounds = new Rectangle(0, sliderTop + (SliderHeight - S(30)) / 2, EffectsWidth - percentWidth - S(8), S(30));
            _percent.Bounds = new Rectangle(EffectsWidth - percentWidth, sliderTop, percentWidth, SliderHeight);
            Bottom = sliderTop + SliderHeight;

            parent.Controls.AddRange([_title, Effects, Brightness, _percent]);
        }

        /// <summary>Shows what the laptop reports: an effect (or -1 for none) and a brightness (or null to leave it).</summary>
        public void Show(int effectIndex, int? brightness)
        {
            Effects.Select(effectIndex);

            // Never under the user's hand: a reading taken mid-drag is already behind.
            if (brightness is { } percent && !Brightness.IsBeingMoved)
                Brightness.Value = percent;
        }
    }
}
