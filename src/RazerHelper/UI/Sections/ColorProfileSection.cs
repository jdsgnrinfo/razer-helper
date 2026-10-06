using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The screen's color profile: Standard, Warm, Cool or Contrast, each applied
/// the moment it is clicked and kept on the screen (see
/// <see cref="ColorProfileService"/>). Reports the choice so the host can save it.
/// </summary>
internal sealed class ColorProfileSection : SectionPanel
{
    private static readonly (ColorProfile Profile, string Label)[] Profiles =
    [
        (ColorProfile.Standard, "Standard"),
        (ColorProfile.Warm, "Warm"),
        (ColorProfile.Cool, "Cool"),
        (ColorProfile.Contrast, "Contrast")
    ];

    private readonly ColorProfileService _service;
    private readonly Label _statusLabel;
    private readonly Dictionary<ColorProfile, Button> _buttons = [];

    public ColorProfileSection(ColorProfileService service)
    {
        _service = service;

        var header = CreateHeaderLayout(50F, 50F);

        // Lined up with the buttons, inside their glow room.
        header.Height = SectionHeaderHeight - GlowRoom;
        header.Padding = new Padding(GlowRoom, 0, GlowRoom, S(12) - GlowRoom);

        _statusLabel = CreateHeaderValueLabel();
        header.Controls.Add(CreateSectionLabel("Color profile", NavIcon.Palette), 0, 0);
        header.Controls.Add(_statusLabel, 1, 0);

        var grid = CreateButtonGrid(Profiles.Select(each => each.Label).ToArray(), "ColorProfileButton", glowRoom: GlowRoom);

        foreach (var button in grid.Controls.OfType<Button>())
        {
            var profile = Profiles.First(each => each.Label == (string)button.Tag!).Profile;
            _buttons[profile] = button;
            button.Font = SemiBoldTitleFont(15);
            button.Click += (_, _) => Choose(profile);
        }

        Controls.Add(grid);
        Controls.Add(header);

        Show(_service.Current, applied: true);
    }

    /// <summary>How tall the section is: its title and a row of large buttons, with their glow room.</summary>
    public static int SectionHeight => SectionHeaderHeight + S(74) + GlowRoom;

    /// <summary>Raised with the profile chosen, after it is applied.</summary>
    public event EventHandler<ColorProfile>? ProfileChosen;

    private void Choose(ColorProfile profile)
    {
        if (profile == _service.Current)
            return;

        var applied = _service.Apply(profile);
        AppLog.Info(applied ? $"Color profile: {profile}." : $"Color profile {profile} was not taken by every screen.");
        Show(profile, applied);
        ProfileChosen?.Invoke(this, profile);
    }

    // Lights the chosen profile; the line at the right says when a screen did not take it.
    private void Show(ColorProfile profile, bool applied)
    {
        HighlightSelected(_buttons.Values, _buttons[profile]);
        _statusLabel.Text = applied ? string.Empty : L.T("This screen does not take it");
    }
}
