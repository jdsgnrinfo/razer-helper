using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI;
using RazerHelper.UI.Forms;

namespace RazerHelper
{
    internal static class Program
    {
        // Session-local, so each signed-in user can run their own tray app.
        private const string SingleInstanceMutexName = @"Local\RazerHelper.SingleInstance";

        [STAThread]
        static int Main(string[] args)
        {
            // The elevated helper that stops or restores Razer's services. It
            // does its one job and exits, so it must come before the
            // single-instance check (the normal app is already running) and
            // before any window exists.
            if (RazerServiceCommand.TryRun(args, new WindowsServiceControl()) is int exitCode)
                return exitCode;

            // The same for clearing the memory cache, from the Optimize window.
            if (MemoryCacheCommand.TryRun(args, WindowsMemoryCache.Purge) is int cacheExitCode)
                return cacheExitCode;

            // And for turning hibernation off or on, from the same window.
            if (HibernationCommand.TryRun(args, WindowsHibernation.SetEnabled) is int hibernationExitCode)
                return hibernationExitCode;

            // After a restart from Settings, let the old copy finish exiting first:
            // it still holds the single-instance lock below until it is gone.
            AppRestart.WaitForPreviousCopy(args);

            // Two instances would compete for the shared HID command channel
            // and overwrite each other's settings file.
            using var singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                SingleInstanceMutexName,
                out var isFirstInstance);

            if (!isFirstInstance)
            {
                AppLog.Info("Another RazerHelper is already running for this user; exiting.");
                return 0;
            }

            AppDiagnostics.LogStart();
            AppDiagnostics.InstallExceptionHandling(ShowError);

            ApplicationConfiguration.Initialize();

            // The size and the language have to be known before any window (or font) is made.
            var settings = new SettingsService().Load();
            UiTheme.SetScale(UiScale.Resolve(settings.WindowScale, UiTheme.WindowsScale));
            L.Current = L.Resolve(settings.Language, System.Globalization.CultureInfo.CurrentUICulture);

            using var trayPopup = new TrayPopupForm();
            using var trayHost = new TrayIconHost(trayPopup);

            Application.Run();

            AppDiagnostics.LogExit();
            return 0;
        }

        private static void ShowError(string message) =>
            MessageBox.Show(message, "RazerHelper", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
