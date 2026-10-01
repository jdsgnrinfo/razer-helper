using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The small Settings window. Every checkbox applies as soon as it is ticked,
/// so there is nothing to confirm: the window only has a close X (Esc works
/// too), like the popup's.
/// </summary>
internal sealed class SettingsForm : Form
{
    private static int ContentWidth => S(512);
    private static int RowPadding => S(8);
    private static int ActionButtonHeight => S(40);
    private static int ActionButtonGap => S(8);

    private readonly IStartupRegistration _startupRegistration;
    private readonly CheckBox _startAtLoginBox;
    private readonly CheckBox _autoSwitchBox;
    private readonly CheckBox _hideWhenClickedAwayBox;
    private readonly CheckBox _alwaysOnTopBox;
    private readonly CheckBox _profileShortcutsBox;
    private readonly CheckBox _silentWithoutTurboBox;
    private readonly CheckBox _silentEfficiencyBox;
    private readonly CheckBox _closeGpuAppsBox;
    private readonly CheckBox _keyboardOffWithScreenBox;
    private readonly Label _errorLabel;

    private bool _isLoading = true;
    private Form? _anchor;

    public SettingsForm(AppSettings settings, IStartupRegistration startupRegistration)
    {
        _startupRegistration = startupRegistration;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont(FontFamilyName, 9F);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true; // Esc closes, as the old Close button's Cancel role did.
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = L.T("RazerHelper Settings");

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = S(new Padding(24)),
            WrapContents = false
        };

        layout.Controls.Add(WindowTitleRow.Create(this, "Settings", ContentWidth));
        layout.Controls.Add(CreateLanguageRow());

        _startAtLoginBox = AddOption(layout, "Start at login", "Opens in the tray when you sign in.");
        _autoSwitchBox = AddOption(layout, "Switch profile with the charger", "Changes mode when you plug in or unplug.");
        _hideWhenClickedAwayBox = AddOption(layout, "Hide when clicking away", "Off, it stays open until you click the tray icon.");
        _alwaysOnTopBox = AddOption(layout, "Always on top", L.F("Stays above other windows and games. {0} shows or hides it.", GlobalHotkey.Text));
        _profileShortcutsBox = AddOption(layout, "Performance mode shortcuts", "Ctrl+Shift+F1 and F2 switch to Balanced and Silent.");
        _silentWithoutTurboBox = AddOption(layout, "Silent without turbo", "In Silent, the CPU stays at its base frequency: cooler and quieter.");
        _silentEfficiencyBox = AddOption(layout, "Efficient Silent", "In Silent, the CPU favors saving energy over speed.");
        _closeGpuAppsBox = AddOption(layout, "Free up GPU when unplugged", "Offers to close apps using the dedicated GPU, to save battery.");
        _keyboardOffWithScreenBox = AddOption(layout, "Keyboard off with the screen", "Turns the lighting off and back on with the screen.");

        _errorLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.IndianRed,
            Margin = new Padding(0, RowPadding, 0, 0),
            MaximumSize = new Size(ContentWidth, 0),
            Visible = false
        };
        layout.Controls.Add(_errorLabel);

        // The actions in one row: Razer's drivers across half of it, then the
        // log folder and the reset (which asks first) sharing the other half.
        layout.Controls.Add(CreateActionRow(
            ("Razer drivers and support", ExternalLinks.OpenRazerDrivers, 2),
            ("Logs", ExternalLinks.OpenLogFolder, 1),
            ("Reset", ConfirmReset, 1)));

        Controls.Add(layout);

        _autoSwitchBox.Checked = settings.AutoSwitchProfiles;
        _hideWhenClickedAwayBox.Checked = settings.HideWhenClickedAway;
        _alwaysOnTopBox.Checked = settings.AlwaysOnTop;
        _profileShortcutsBox.Checked = settings.ProfileShortcuts;
        _silentWithoutTurboBox.Checked = settings.SilentWithoutTurbo;
        _silentEfficiencyBox.Checked = settings.SilentEfficiency;
        _closeGpuAppsBox.Checked = settings.CloseGpuAppsOnUnplug;
        _keyboardOffWithScreenBox.Checked = settings.KeyboardOffWithScreen;
        _startAtLoginBox.Checked = ReadStartAtLogin();

        _startAtLoginBox.CheckedChanged += StartAtLoginBox_CheckedChanged;
        _autoSwitchBox.CheckedChanged += (_, _) => AutoSwitchProfilesChanged?.Invoke(this, _autoSwitchBox.Checked);
        _hideWhenClickedAwayBox.CheckedChanged += (_, _) => HideWhenClickedAwayChanged?.Invoke(this, _hideWhenClickedAwayBox.Checked);
        _alwaysOnTopBox.CheckedChanged += (_, _) => AlwaysOnTopChanged?.Invoke(this, _alwaysOnTopBox.Checked);
        _profileShortcutsBox.CheckedChanged += (_, _) => ProfileShortcutsChanged?.Invoke(this, _profileShortcutsBox.Checked);
        _silentWithoutTurboBox.CheckedChanged += (_, _) => SilentWithoutTurboChanged?.Invoke(this, _silentWithoutTurboBox.Checked);
        _silentEfficiencyBox.CheckedChanged += (_, _) => SilentEfficiencyChanged?.Invoke(this, _silentEfficiencyBox.Checked);
        _closeGpuAppsBox.CheckedChanged += (_, _) => CloseGpuAppsOnUnplugChanged?.Invoke(this, _closeGpuAppsBox.Checked);
        _keyboardOffWithScreenBox.CheckedChanged += (_, _) => KeyboardOffWithScreenChanged?.Invoke(this, _keyboardOffWithScreenBox.Checked);

        _isLoading = false;
    }

    public event EventHandler<bool>? AutoSwitchProfilesChanged;

    public event EventHandler<bool>? HideWhenClickedAwayChanged;

    public event EventHandler<bool>? CloseGpuAppsOnUnplugChanged;

    public event EventHandler<bool>? AlwaysOnTopChanged;

    public event EventHandler<bool>? ProfileShortcutsChanged;

    public event EventHandler<bool>? SilentWithoutTurboChanged;

    public event EventHandler<bool>? SilentEfficiencyChanged;

    public event EventHandler<bool>? KeyboardOffWithScreenChanged;

    /// <summary>True when the user confirmed a reset; the window then closes and the caller carries it out.</summary>
    public bool ResetConfirmed { get; private set; }

    /// <summary>A language other than the current one, when the user picked it; the window then closes and the caller restarts the app in it.</summary>
    public AppLanguage? LanguageChosen { get; private set; }

    private bool ReadStartAtLogin()
    {
        try
        {
            return _startupRegistration.IsEnabled;
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read the start-at-login setting.", exception);
            return false;
        }
    }

    private void StartAtLoginBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isLoading)
            return;

        var wanted = _startAtLoginBox.Checked;

        try
        {
            _startupRegistration.SetEnabled(wanted);
            _errorLabel.Visible = false;
            AppLog.Info($"Start at login turned {(wanted ? "on" : "off")}.");
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not change the start-at-login setting.", exception);

            // Show what is really true, and say so.
            _isLoading = true;
            _startAtLoginBox.Checked = !wanted;
            _isLoading = false;

            _errorLabel.Text = L.T("Could not change Start at login.");
            _errorLabel.Visible = true;
        }
    }

    // Says exactly what will change before anything does. "No" is the default.
    private void ConfirmReset()
    {
        var answer = MessageBox.Show(
            this,
            L.T("Reset RazerHelper to how it was the first time you opened it?\r\n\r\n" +
                "This will:\r\n" +
                "  - clear your saved settings, including the options in this window and your never-close list\r\n" +
                "  - turn off Start at login\r\n" +
                "  - set the laptop to Balanced mode with no battery charge limit\r\n\r\n" +
                "It will not change Razer's background services or anything else on your PC. RazerHelper will restart."),
            L.T("Reset to defaults"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
            return;

        ResetConfirmed = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    // One setting as Windows 11 lays it out: the title and, under it, what it
    // does, wrapping onto more lines as needed, on the left; the switch on the
    // right, centered on them. Clicking anywhere on the row flips the switch.
    private static CheckBox AddOption(FlowLayoutPanel layout, string text, string hint)
    {
        var toggle = new ToggleSwitch
        {
            AccessibleName = L.T(text),
            Anchor = AnchorStyles.Right
        };

        var card = CreateCard(text, hint, toggle);

        // The card, the text block and each text in it, but not the switch,
        // which flips itself.
        var words = card.GetControlFromPosition(0, 0)!;

        foreach (var part in words.Controls.Cast<Control>().Append(words).Append(card))
        {
            part.Click += (_, _) => toggle.Checked = !toggle.Checked;
            part.Cursor = Cursors.Hand;
        }

        layout.Controls.Add(CreateDivider());
        layout.Controls.Add(card);
        return toggle;
    }

    // A setting's row, 12px above and below: the title and, under it, what it
    // does in quiet grey, on the left, however long the texts run; the control
    // at the right edge, 24px clear of them.
    private static TableLayoutPanel CreateCard(string text, string hint, Control control)
    {
        control.Margin = Padding.Empty;

        var words = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            WrapContents = false
        };

        words.Controls.Add(new Label
        {
            AutoSize = true,
            Font = SemiBoldFont(16),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            MaximumSize = new Size(ContentWidth - control.Width - S(24), 0),
            Text = L.T(text)
        });

        words.Controls.Add(new Label
        {
            AutoSize = true,
            Font = DesignFont(14),
            ForeColor = SubtleTextColor,
            Margin = new Padding(0, S(2), 0, 0),
            MaximumSize = new Size(ContentWidth - control.Width - S(24), 0),
            Text = L.T(hint)
        });

        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, RowPadding, 0, RowPadding),
            RowCount = 1
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth - control.Width));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(words, 0, 0);
        row.Controls.Add(control, 1, 0);
        return row;
    }

    // The thin line between two settings.
    private static Control CreateDivider() => new Panel
    {
        BackColor = DividerColor,
        Height = S(1),
        Margin = Padding.Empty,
        Width = ContentWidth
    };

    // "Language" on the left and the list on the right. Each language is named
    // in itself, so it can be found whatever the current one is. Picking a
    // different one closes the window; the popup then restarts the app in it.
    private Control CreateLanguageRow()
    {
        var languages = Enum.GetValues<AppLanguage>();

        var dropdown = new DropdownButton([.. languages.Select(LanguageName)])
        {
            Anchor = AnchorStyles.Right,
            Font = TitleFont(16, FontStyle.Bold),
            Size = S(new Size(116, 38))
        };

        dropdown.Select(Array.IndexOf(languages, L.Current));
        dropdown.SelectionChanged += (_, _) =>
        {
            var chosen = languages[dropdown.SelectedIndex];

            if (chosen == L.Current)
                return;

            LanguageChosen = chosen;
            Close();
        };

        var card = CreateCard("Language", "RazerHelper restarts to change the language.", dropdown);
        card.Margin = new Padding(0, S(12), 0, 0);
        return card;
    }

    private static string LanguageName(AppLanguage language) => language switch
    {
        AppLanguage.Spanish => "Español",
        _ => "English"
    };

    // One row of the app's buttons, each taking its share of the content
    // width (a weight of 2 is twice as wide as 1), 8px apart.
    private static Control CreateActionRow(params (string Text, Action Open, int Weight)[] actions)
    {
        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = actions.Length,
            Height = ActionButtonHeight,
            Margin = new Padding(0, S(8), 0, 0),
            Padding = Padding.Empty,
            RowCount = 1,
            Width = ContentWidth
        };

        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var gaps = ActionButtonGap * (actions.Length - 1);
        var share = (ContentWidth - gaps) / (float)actions.Sum(action => action.Weight);

        for (var index = 0; index < actions.Length; index++)
        {
            var (text, open, weight) = actions[index];
            var last = index == actions.Length - 1;

            row.ColumnStyles.Add(last
                ? new ColumnStyle(SizeType.Percent, 100F) // The rest, so rounding never leaves a sliver.
                : new ColumnStyle(SizeType.Absolute, share * weight + ActionButtonGap));

            var button = CreateActionButton(text);
            button.Margin = new Padding(0, 0, last ? 0 : ActionButtonGap, 0);
            button.Click += (_, _) => open();
            row.Controls.Add(button, index, 0);
        }

        return row;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
            Close();
    }

    /// <summary>Opens next to this window (the popup) instead of centered over it, where it would hide it.</summary>
    public void PlaceBeside(Form anchor)
    {
        StartPosition = FormStartPosition.Manual;
        _anchor = anchor;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_anchor is null)
            return;

        // The size is only final once the layout has run.
        PerformLayout();
        var workingArea = Screen.FromRectangle(_anchor.Bounds).WorkingArea;
        Location = WindowPlacement.Beside(_anchor.Bounds, Size, workingArea);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }
}
