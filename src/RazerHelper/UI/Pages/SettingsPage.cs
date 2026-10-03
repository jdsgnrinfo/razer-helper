using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The app's own settings: the language, starting at login, how the window
/// behaves and the mode shortcuts, then Razer's drivers, the logs and the
/// reset. What controls the laptop lives in its own section. Every switch
/// applies as soon as it is flipped, so there is nothing to confirm.
/// </summary>
internal sealed class SettingsPage : PageView
{
    private static int ButtonGap => S(8);

    private readonly IStartupRegistration _startupRegistration;
    private readonly ToggleSwitch _startAtLoginBox;
    private readonly ToggleSwitch _hideWhenClickedAwayBox;
    private readonly ToggleSwitch _alwaysOnTopBox;
    private readonly ToggleSwitch _profileShortcutsBox;
    private readonly Label _errorLabel;

    private bool _isLoading = true;

    public SettingsPage(AppSettings settings, IStartupRegistration startupRegistration)
    {
        _startupRegistration = startupRegistration;

        Add(CreateLanguageRow());

        _startAtLoginBox = AddOption("Start at login", "Opens in the tray when you sign in.");
        _hideWhenClickedAwayBox = AddOption("Hide when clicking away", "Off, it stays open until you click the tray icon.");
        _alwaysOnTopBox = AddOption("Always on top", L.F("Stays above other windows and games. {0} shows or hides it.", GlobalHotkey.Text));
        _profileShortcutsBox = AddOption("Performance mode shortcuts", "Ctrl+Shift+F1 and F2 switch to Balanced and Silent.");

        _errorLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.IndianRed,
            Margin = new Padding(0, S(12), 0, 0),
            MaximumSize = new Size(ContentWidth, 0),
            Visible = false
        };
        Add(_errorLabel);

        // Razer's drivers, the log folder and the reset (which asks first), as one setting.
        Add(CreateDivider());
        Add(CreateCard("Maintenance", "Razer drivers and support, logs, reset.", CreateButtonRow(
            ("Drivers", ExternalLinks.OpenRazerDrivers),
            ("Logs", ExternalLinks.OpenLogFolder),
            ("Reset", ConfirmReset))));

        // The app's version, quietly, at the foot of its settings.
        Add(new Label
        {
            AutoSize = true,
            Font = DesignFont(13),
            ForeColor = Color.FromArgb(0x6E, 0x6E, 0x6E),
            Margin = new Padding(0, S(16), 0, 0),
            Text = AppVersion.Current.ToUpperInvariant()
        });

        _hideWhenClickedAwayBox.Checked = settings.HideWhenClickedAway;
        _alwaysOnTopBox.Checked = settings.AlwaysOnTop;
        _profileShortcutsBox.Checked = settings.ProfileShortcuts;
        _startAtLoginBox.Checked = ReadStartAtLogin();

        _startAtLoginBox.CheckedChanged += StartAtLoginBox_CheckedChanged;
        _hideWhenClickedAwayBox.CheckedChanged += (_, _) => HideWhenClickedAwayChanged?.Invoke(this, _hideWhenClickedAwayBox.Checked);
        _alwaysOnTopBox.CheckedChanged += (_, _) => AlwaysOnTopChanged?.Invoke(this, _alwaysOnTopBox.Checked);
        _profileShortcutsBox.CheckedChanged += (_, _) => ProfileShortcutsChanged?.Invoke(this, _profileShortcutsBox.Checked);

        _isLoading = false;
    }

    public event EventHandler<bool>? HideWhenClickedAwayChanged;

    public event EventHandler<bool>? AlwaysOnTopChanged;

    public event EventHandler<bool>? ProfileShortcutsChanged;

    /// <summary>Raised when the user confirmed a reset; the window carries it out.</summary>
    public event EventHandler? ResetConfirmed;

    /// <summary>Raised with a language other than the current one; the window restarts the app in it.</summary>
    public event EventHandler<AppLanguage>? LanguageChosen;

    // Windows can change Start at login meanwhile (Task Manager's Startup apps).
    public override void OnPageShown()
    {
        _isLoading = true;
        _startAtLoginBox.Checked = ReadStartAtLogin();
        _isLoading = false;
    }

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
        DialogResult answer;

        using (KeepOpen())
        {
            answer = MessageBox.Show(
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
        }

        if (answer == DialogResult.Yes)
            ResetConfirmed?.Invoke(this, EventArgs.Empty);
    }

    private ToggleSwitch AddOption(string text, string hint)
    {
        var card = CreateSwitchCard(text, hint, out var toggle);
        Add(CreateDivider());
        Add(card);
        return toggle;
    }

    // "Language" on the left and the list on the right. Each language is named
    // in itself, so it can be found whatever the current one is. Picking a
    // different one restarts the app in it.
    private Control CreateLanguageRow()
    {
        var languages = Enum.GetValues<AppLanguage>();

        var list = new DropdownButton([.. languages.Select(LanguageName)])
        {
            Anchor = AnchorStyles.Right,
            Font = SemiBoldTitleFont(16),
            Size = S(new Size(116, 38))
        };

        list.Select(Array.IndexOf(languages, L.Current));
        list.SelectionChanged += (_, _) =>
        {
            var chosen = languages[list.SelectedIndex];

            if (chosen != L.Current)
                LanguageChosen?.Invoke(this, chosen);
        };

        return CreateCard("Language", "RazerHelper restarts to change the language.", list);
    }

    private static string LanguageName(AppLanguage language) => language switch
    {
        AppLanguage.Spanish => "Español",
        _ => "English"
    };

    // Small buttons side by side, each sized to its text, 8px apart.
    private static Control CreateButtonRow(params (string Text, Action Open)[] actions)
    {
        var row = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            WrapContents = false
        };

        for (var index = 0; index < actions.Length; index++)
        {
            var (text, open) = actions[index];
            var button = CreateSmallButton(text);
            button.Height = S(38);
            button.Margin = new Padding(index == 0 ? 0 : ButtonGap, 0, 0, 0);
            button.Click += (_, _) => open();
            row.Controls.Add(button);
        }

        // Sized now: the card makes room for it by its width.
        row.PerformLayout();
        row.Size = row.GetPreferredSize(Size.Empty);
        return row;
    }
}
