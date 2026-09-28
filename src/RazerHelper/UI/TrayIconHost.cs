using RazerHelper.Core.Localization;
using RazerHelper.Core.Models;
using RazerHelper.Helpers;
using RazerHelper.UI.Forms;

namespace RazerHelper.UI;

public sealed class TrayIconHost : IDisposable
{
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayPopupForm _popup;
    private readonly TrayIconArt _art;
    private PerformanceMode? _shownMode;

    public TrayIconHost(TrayPopupForm popup)
    {
        _popup = popup;
        _art = new TrayIconArt(LoadTrayIcon());

        _menu = new ContextMenuStrip();
        _menu.Items.Add(L.T("Open RazerHelper"), null, (_, _) => TogglePopup());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(L.T("Exit"), null, (_, _) => ExitApplication());

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _art.For(null),
            Text = "RazerHelper",
            Visible = true
        };

        _notifyIcon.MouseClick += OnTrayIconMouseClick;
        _popup.PerformanceModeChanged += OnPerformanceModeChanged;
    }

    /// <summary>What the icon says on hover: "RazerHelper (Gaming)", or just the name while the mode is unknown.</summary>
    internal static string HoverText(PerformanceMode? mode) =>
        mode is PerformanceMode known ? $"RazerHelper ({L.T(known.ToString())})" : "RazerHelper";

    // The icon takes the mode's color and names it on hover. Nothing is
    // redrawn when the mode is the one already shown.
    private void OnPerformanceModeChanged(object? sender, PerformanceMode? mode)
    {
        if (mode == _shownMode)
            return;

        _shownMode = mode;
        _notifyIcon.Icon = _art.For(mode);
        _notifyIcon.Text = HoverText(mode);
    }

    // The icon file holds several sizes; ask for the one the tray uses at this
    // display scale (16 px at 100%, larger on high-density screens).
    private static Icon LoadTrayIcon()
    {
        try
        {
            using var stream = typeof(TrayIconHost).Assembly.GetManifestResourceStream("RazerHelper.ico");

            if (stream is not null)
                return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            RazerHelper.Core.Diagnostics.AppLog.Error("Could not load the tray icon.", exception);
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _popup.PerformanceModeChanged -= OnPerformanceModeChanged;
        _notifyIcon.MouseClick -= OnTrayIconMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _art.Dispose();
        _menu.Dispose();
    }

    private void OnTrayIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            TogglePopup();
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            // Fades out first; RequestHide does nothing while a fade-out runs,
            // so a double click cannot interrupt it.
            _popup.RequestHide();
            return;
        }

        // The click that just closed it by taking its focus is not a request to open it again.
        if (_popup.WasJustHidden)
            return;

        _popup.Location = TaskbarPlacement.GetPopupLocation(_popup.Size);
        _popup.Show();
        _popup.Activate();
    }

    private void ExitApplication()
    {
        _popup.CloseForApplicationExit();
        Application.ExitThread();
    }
}
