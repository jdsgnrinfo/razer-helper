using System.ServiceProcess;

namespace RazerHelper.Core.Models;

internal sealed record AppSettings(
    string? DisplayMode = null,
    int? BatteryChargeLimit = null,
    PowerProfile? PluggedInProfile = null,
    PowerProfile? OnBatteryProfile = null,
    // What each Razer service was set to before "Stop" disabled it, so "Start"
    // can put back exactly that instead of guessing Automatic.
    Dictionary<string, ServiceStartMode>? RazerServiceStartModes = null,
    // Written by earlier versions, before profiles existed. Read once and
    // folded into PluggedInProfile by SettingsService, then dropped.
    string? PerformanceMode = null,
    string? CustomCpuBoost = null,
    string? CustomGpuBoost = null,
    // Settings window. Both are on unless the user turns them off, which is
    // also what a settings file from an earlier version means.
    bool AutoSwitchProfiles = true,
    bool HideWhenClickedAway = true,
    // Off unless the user turns it on: it closes other programs.
    bool CloseGpuAppsOnUnplug = false,
    // Program names (e.g. "blender") to never close, on top of the built-in
    // list. Edited by hand in settings.json.
    string[]? NeverCloseApps = null,
    // What Task Manager's startup switch was for each Razer login entry before
    // "Stop" turned it off (as stored text; empty means it had none), so "Start"
    // can put back exactly that.
    Dictionary<string, string>? RazerLoginApprovals = null,
    // Off unless the user turns it on: the window stays above other windows
    // (borderless-window games included).
    bool AlwaysOnTop = false,
    // How much bigger than the base design the window is drawn (1 = base size).
    // Null picks a size from Windows' display scaling. Edited by hand in
    // settings.json, for example 1.5; applies the next time the app starts.
    double? WindowScale = null,
    // On unless the user turns it off: the keyboard backlight goes off while
    // the screen is off (display timeout, sleep, closed lid).
    bool KeyboardOffWithScreen = true,
    // On unless the user turns it off: Ctrl+Shift+F1/F2/F3 switch performance
    // mode from any program. Off frees them for another program that wants them.
    bool ProfileShortcuts = true,
    // The interface language: "en" or "es". Null follows the Windows display
    // language. Changing it restarts the app.
    string? Language = null);
