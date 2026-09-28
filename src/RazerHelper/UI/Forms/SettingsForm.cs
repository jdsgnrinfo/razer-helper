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
    private static int ContentWidth => S(400);
    private static int ActionButtonHeight => S(32);
    private static int ActionButtonGap => S(8);

    private readonly IStartupRegistration _startupRegistration;
    private readonly CheckBox _startAtLoginBox;
    private readonly CheckBox _autoSwitchBox;
    private readonly CheckBox _hideWhenClickedAwayBox;
    private readonly CheckBox _alwaysOnTopBox;
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
        Font = GetDesignFont("Segoe UI", 9F);
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
            Padding = S(new Padding(16, 14, 16, 14)),
            WrapContents = false
        };

        layout.Controls.Add(WindowTitleRow.Create(this, "Settings", ContentWidth));
        layout.Controls.Add(CreateLanguageRow());

        _startAtLoginBox = AddOption(layout, "Start at login", "Opens in the tray when you sign in.");
        _autoSwitchBox = AddOption(layout, "Switch profile with the charger", "Changes mode when you plug in or unplug.");
        _hideWhenClickedAwayBox = AddOption(layout, "Hide when clicking away", "Off, it stays open until you click the tray icon.");
        _alwaysOnTopBox = AddOption(layout, "Always on top", L.F("Stays above other windows and games. {0} shows or hides it.", GlobalHotkey.Text));
        _closeGpuAppsBox = AddOption(layout, "Free up GPU when unplugged", "Offers to close apps using the dedicated GPU, to save battery.");
        _keyboardOffWithScreenBox = AddOption(layout, "Keyboard off with the screen", "Turns the lighting off and back on with the screen.");

        _errorLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.IndianRed,
            Margin = S(new Padding(0, 0, 0, 6)),
            MaximumSize = new Size(ContentWidth, 0),
            Visible = false
        };
        layout.Controls.Add(_errorLabel);

        // The actions: Razer's drivers on their own full-width row, then the
        // log folder and the reset side by side.
        var driversRow = CreateActionRow(("Razer drivers and support", ExternalLinks.OpenRazerDrivers, null));
        driversRow.Margin = new Padding(0, S(8), 0, ActionButtonGap);
        layout.Controls.Add(driversRow);
        layout.Controls.Add(CreateActionRow(
            ("Open log folder", ExternalLinks.OpenLogFolder, null),
            ("Reset to defaults...", ConfirmReset, Color.IndianRed)));

        Controls.Add(layout);

        _autoSwitchBox.Checked = settings.AutoSwitchProfiles;
        _hideWhenClickedAwayBox.Checked = settings.HideWhenClickedAway;
        _alwaysOnTopBox.Checked = settings.AlwaysOnTop;
        _closeGpuAppsBox.Checked = settings.CloseGpuAppsOnUnplug;
        _keyboardOffWithScreenBox.Checked = settings.KeyboardOffWithScreen;
        _startAtLoginBox.Checked = ReadStartAtLogin();

        _startAtLoginBox.CheckedChanged += StartAtLoginBox_CheckedChanged;
        _autoSwitchBox.CheckedChanged += (_, _) => AutoSwitchProfilesChanged?.Invoke(this, _autoSwitchBox.Checked);
        _hideWhenClickedAwayBox.CheckedChanged += (_, _) => HideWhenClickedAwayChanged?.Invoke(this, _hideWhenClickedAwayBox.Checked);
        _alwaysOnTopBox.CheckedChanged += (_, _) => AlwaysOnTopChanged?.Invoke(this, _alwaysOnTopBox.Checked);
        _closeGpuAppsBox.CheckedChanged += (_, _) => CloseGpuAppsOnUnplugChanged?.Invoke(this, _closeGpuAppsBox.Checked);
        _keyboardOffWithScreenBox.CheckedChanged += (_, _) => KeyboardOffWithScreenChanged?.Invoke(this, _keyboardOffWithScreenBox.Checked);

        _isLoading = false;
    }

    public event EventHandler<bool>? AutoSwitchProfilesChanged;

    public event EventHandler<bool>? HideWhenClickedAwayChanged;

    public event EventHandler<bool>? CloseGpuAppsOnUnplugChanged;

    public event EventHandler<bool>? AlwaysOnTopChanged;

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

    // One setting as Windows 11 lays it out, on a card of its own: the title
    // and, under it, what it does, wrapping onto more lines as needed, on the
    // left; the switch on the right, centered on them. Clicking anywhere on
    // the card flips the switch.
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

        layout.Controls.Add(card);
        return toggle;
    }

    // A card with a title and a description on the left and a control on the
    // right, in the content width, however long the texts run.
    private static CardPanel CreateCard(string text, string hint, Control control)
    {
        control.Margin = new Padding(S(12), 0, 0, 0);

        var padding = S(new Padding(14, 11, 12, 11));
        var textWidth = ContentWidth - padding.Horizontal - control.Width - control.Margin.Horizontal;

        var words = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = CardColor,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            WrapContents = false
        };

        words.Controls.Add(new Label
        {
            AutoSize = true,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            MaximumSize = new Size(textWidth, 0),
            Text = L.T(text)
        });

        var hintLabel = CreateHint(hint);
        hintLabel.Margin = S(new Padding(0, 2, 0, 0));
        hintLabel.MaximumSize = new Size(textWidth, 0);
        words.Controls.Add(hintLabel);

        var card = new CardPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = S(new Padding(0, 0, 0, 6)),
            Padding = padding,
            RowCount = 1
        };

        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, textWidth));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.Controls.Add(words, 0, 0);
        card.Controls.Add(control, 1, 0);
        return card;
    }

    private static Label CreateHint(string hint) => new()
    {
        AutoSize = true,
        Font = GetDesignFont("Segoe UI", 8F),
        ForeColor = SubtleTextColor,
        Margin = new Padding(0, 0, 0, S(6)),
        MaximumSize = new Size(ContentWidth, 0),
        Text = L.T(hint)
    };

    // "Language" on the left and the list on the right. Each language is named
    // in itself, so it can be found whatever the current one is. Picking a
    // different one closes the window; the popup then restarts the app in it.
    private Control CreateLanguageRow()
    {
        var languages = Enum.GetValues<AppLanguage>();

        // The buttons' color, lighter than the card, so it stands out on it.
        var dropdown = new DropdownButton([.. languages.Select(LanguageName)])
        {
            Anchor = AnchorStyles.Right,
            Size = S(new Size(130, 30))
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

        return CreateCard("Language", "RazerHelper restarts to change the language.", dropdown);
    }

    private static string LanguageName(AppLanguage language) => language switch
    {
        AppLanguage.Spanish => "Español",
        _ => "English"
    };

    // A row of the app's rounded buttons, sharing the content width equally.
    private static Control CreateActionRow(params (string Text, Action Open, Color? TextColor)[] actions)
    {
        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = actions.Length,
            Height = ActionButtonHeight,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1,
            Width = ContentWidth
        };

        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < actions.Length; index++)
        {
            var (text, open, textColor) = actions[index];

            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / actions.Length));

            var button = CreateActionButton(text);
            button.ForeColor = textColor ?? Color.White;
            button.Margin = new Padding(index == 0 ? 0 : ActionButtonGap / 2, 0, index == actions.Length - 1 ? 0 : ActionButtonGap / 2, 0);
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
