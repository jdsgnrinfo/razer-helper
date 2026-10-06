using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Sections;

/// <summary>
/// The screen's color profile, on one line: its title at the left and a list
/// of the profiles at the right (Standard, Warm, Cool or Contrast), each
/// applied the moment it is picked and kept on the screen (see
/// <see cref="ColorProfileService"/>); before the list, a note when a screen
/// did not take it. Reports the choice so the host can save it.
/// </summary>
internal sealed class ColorProfileSection : SectionPanel
{
    private static readonly ColorProfile[] Profiles = [ColorProfile.Standard, ColorProfile.Warm, ColorProfile.Cool, ColorProfile.Contrast];

    private readonly ColorProfileService _service;
    private readonly Label _title;
    private readonly Label _statusLabel;
    private readonly DropdownButton _list;

    public ColorProfileSection(ColorProfileService service)
    {
        _service = service;

        _title = CreateSectionLabel("Color profile", NavIcon.Palette);
        _title.Dock = DockStyle.None;
        _title.BackColor = BackgroundColor;

        _statusLabel = CreateHeaderValueLabel();
        _statusLabel.Dock = DockStyle.None;
        _statusLabel.BackColor = BackgroundColor;

        _list = new DropdownButton([.. Profiles.Select(ProfileName)])
        {
            AccessibleName = L.T("Color profile"),
            Font = SemiBoldTitleFont(16),
            Size = S(new Size(190, 38))
        };
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedIndex >= 0)
                Choose(Profiles[_list.SelectedIndex]);
        };

        Controls.Add(_title);
        Controls.Add(_statusLabel);
        Controls.Add(_list);
        Layout += (_, _) => Arrange();

        Show(_service.Current, applied: true);
    }

    /// <summary>How tall the section is: one line, as tall as the list.</summary>
    public static int SectionHeight => S(38);

    /// <summary>Raised with the profile chosen, after it is applied.</summary>
    public event EventHandler<ColorProfile>? ProfileChosen;

    // The title at the content's left, the list at its right edge, the note just before the list; all centred on the line.
    // The section is as wide as the page's wide parts: the content starts and ends the glow room in.
    private void Arrange()
    {
        var left = GlowRoom;
        var right = Width - GlowRoom;

        _title.Location = new Point(left, (Height - _title.Height) / 2);
        _list.Location = new Point(right - _list.Width, (Height - _list.Height) / 2);

        var status = _statusLabel.PreferredSize;
        _statusLabel.Size = status;
        _statusLabel.Location = new Point(_list.Left - S(14) - status.Width, (Height - status.Height) / 2);
    }

    private void Choose(ColorProfile profile)
    {
        if (profile == _service.Current)
            return;

        var applied = _service.Apply(profile);
        AppLog.Info(applied ? $"Color profile: {profile}." : $"Color profile {profile} was not taken by every screen.");
        Show(profile, applied);
        ProfileChosen?.Invoke(this, profile);
    }

    // Shows the chosen profile; the note says when a screen did not take it.
    private void Show(ColorProfile profile, bool applied)
    {
        _list.Select(Array.IndexOf(Profiles, profile));
        _statusLabel.Text = applied ? string.Empty : L.T("This screen does not take it");
        Arrange();
    }

    private static string ProfileName(ColorProfile profile) => profile switch
    {
        ColorProfile.Warm => L.T("Warm"),
        ColorProfile.Cool => L.T("Cool"),
        ColorProfile.Contrast => L.T("Contrast"),
        _ => L.T("Standard")
    };
}
