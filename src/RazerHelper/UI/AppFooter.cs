using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The very bottom of the popup: the app version (just its number) on the
/// left, then the "System info", "Free up GPU" and Settings buttons on the
/// right. It only reports clicks; the popup decides what they do.
/// </summary>
internal sealed class AppFooter : TableLayoutPanel
{
    // Between the small buttons.
    private static int SmallButtonGap => S(8);

    public AppFooter()
    {
        BackColor = BackgroundColor;
        ColumnCount = 4;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        RowCount = 1;

        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = DesignFont(14),
            ForeColor = Color.FromArgb(0x6E, 0x6E, 0x6E),
            Margin = Padding.Empty,
            Text = AppVersion.Current.ToUpperInvariant(),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var systemInfoButton = CreateSmallButton("System info");
        systemInfoButton.Anchor = AnchorStyles.Right;
        systemInfoButton.Margin = new Padding(0, 0, SmallButtonGap, 0);
        systemInfoButton.Click += (_, _) => SystemInfoRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(systemInfoButton, 1, 0);

        var freeUpButton = CreateSmallButton("Free up GPU");
        freeUpButton.Anchor = AnchorStyles.Right;
        freeUpButton.Margin = new Padding(0, 0, SmallButtonGap, 0);
        freeUpButton.Click += (_, _) => FreeUpGpuRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(freeUpButton, 2, 0);

        var settingsButton = CreateSmallButton("Settings");
        settingsButton.Anchor = AnchorStyles.Right;
        settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(settingsButton, 3, 0);
    }

    public event EventHandler? SystemInfoRequested;

    public event EventHandler? FreeUpGpuRequested;

    public event EventHandler? SettingsRequested;
}
