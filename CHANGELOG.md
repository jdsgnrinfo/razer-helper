# Changelog

## Fork 1.1.0

Changes made in this fork by [jdsgnrinfo](https://github.com/jdsgnrinfo) on top of Paul Rodriguez's [RazerHelper](https://github.com/Paulrod20/razer-helper) 1.0.0. Tested on a **Razer Blade 15 Base (2020)**. Every change can be seen line by line in the [comparison with the original](https://github.com/Paulrod20/razer-helper/compare/main...jdsgnrinfo:razer-helper:main).

### Razer Blade 15 Base (2020)

- **Silent** offered on battery.
- **Max fan** through the model's manual fan method (not in Silent). It is turned off when the app starts and exits, so the fans are never left flat out.
- **Keyboard color:** Static and Breathing in a chosen color. Wave is not offered (single-zone keyboard).
- The charge limit and the max fan flag are shown as unavailable: this firmware only echoes those commands without acting on them.

### Performance modes

- **Tray icon in the mode's color:** green for Balanced, blue for Silent, purple for Custom, repainted from the logo in the app itself; hovering it shows "RazerHelper (Silent)".
- **Shortcuts:** Ctrl+Shift+F1 and F2 switch to Balanced and Silent from any program, even a game. A small notice at the top right shows the mode's icon, name and status without taking the focus, then fades out. A switch in Settings, on by default, turns them off for anyone who does not want them or has another program using those keys.
- **Silent without turbo:** Silent always keeps the CPU at its base frequency by turning off Windows' processor boost in the active power plan; part of the mode, with no switch. It is checked every couple of seconds, so a plan switch (the Idle option, Windows, Razer's software) or a boost put back meanwhile does not bring the turbo back while in Silent. The previous setting is saved and comes back when the laptop goes to Balanced or Custom, not when the app closes. An earlier build also changed the CPU's energy preference in Silent; that is given back once at startup.
- **Idle power plan** (Idle state button, off by default): after 1 to 30 minutes without touching the keyboard or mouse, Windows switches to the power plan you choose, listed from the plans on your PC; your plan comes back exactly as it was as soon as you use the laptop. A video or a game that keeps the screen on counts as using it.
- **Optimize** (green button at the bottom of the window): **Free up memory** moves what background programs are not using out of RAM (the program in front, such as a game, is left alone, and nothing is closed); **Temporary files** shows how much your temp folder holds in files over a day old and clears them, leaving any a program has open; and **Free up GPU**. Each runs only when you press its button.
- **Balanced, Silent and Custom on every model.** Gaming is no longer offered, and Custom takes its game controller icon; a profile saved with Gaming is applied as Custom. A laptop whose firmware does not take a mode reports another one back; the app then marks that button as not supported instead of offering it again.

### Language

- Full **Spanish translation**, chosen in Settings; the app restarts in the chosen language. English stays the default.

### Interface

- **Redesign** of every window: flat sections split by thin lines, section titles in capitals, flat buttons with 6px corners (green with dark text when selected, a touch lighter under the pointer), thin sliders filling in green with a white square thumb, green and grey switches with a white square knob. The main window is 600 wide.
- **Performance modes** as large buttons, quietest first (Silent, Balanced, Custom), each with its icon in a circle.
- **Refresh rate buttons** show an icon instead of text: the rate in square digits over a row of bars ("A" for Auto), drawn for whatever rates the screen offers, in green like the mode icons; the name ("60 Hz", "Auto") appears under the pointer.
- **Quicker to answer:** a performance mode or a refresh rate turns green as soon as it is clicked, without greying the other buttons while the laptop applies it (a failure puts back what was there); the keyboard and logo brightness follow the slider while it is dragged; the lighting lists no longer grey out during a change; and changing the refresh rate no longer freezes the window while the screen re-syncs.
- **Fans** as two options with a line on what each does: Automatic RPM and Max RPM.
- **Lighting** with the keyboard and the logo side by side.
- **Header and footer:** the app's logo and name on the left and the close X on the right; the version and the System info, Free up GPU and Settings buttons at the bottom.
- **Custom window:** the GPU's buttons line up with the CPU's, a line between the two, and the note in a green box.
- **Battery details:** each figure under its name in grey, with lines between them, and flat green bars.
- Every window takes its rounded corners and border from Windows 11.
- **New icons** for the performance modes: Balanced and Silent designed for this fork, Custom and the CPU and GPU from open icon sets (see the credits in the README).
- CPU and GPU temperatures in the Performance header, as "CPU: 52°C | GPU: 45°C".
- The charge limit slider shows the chosen limit at its right, like the lighting sliders.
- **Any percentage on every slider:** the charge limit takes any value from 60% to 100% (it offered only 60, 80 and 100) and the lighting brightness any value from 0% to 100% (it went in steps of 5). The thumb follows the pointer while dragging and glides into place on release.
- **The keyboard brightness slider follows the Fn keys:** while the window is open, changing the keyboard brightness with the laptop's Fn keys moves the slider too.
- **Windows beside the main one no longer block it:** Battery, System information, Custom and Settings open beside the main window, which can still be used. Only one is open at a time: opening another closes the one before.
- **Dark tray menu:** the tray icon's right-click menu matches the app: dark rounded panel, white text, a highlight across the whole width under the pointer.
- **Smoother motion:** hover changes, switches and the fades of the window and the shortcut notice all take 300 ms, easing in and out.
- **New typeface:** the interface is set in Titillium Web, which comes inside the app, so nothing needs installing; Segoe UI is the fallback.

### New windows

- **Custom:** the CPU and GPU boost levels in their own window beside the popup, with live CPU temperature, usage and speed, and GPU temperature, usage, core clock and memory clock. The GPU's usage and core clock come from NVIDIA's library, asked only while the GPU is awake. The window closes by itself if the laptop leaves Custom.
- **Battery details** (More info): power in or out, time left or to full, charge, health against the design capacity, voltage and the battery itself.
- **System information** (System info, in the footer): Windows, CPU, integrated and dedicated GPU, RAM in use, each drive letter with its drive and free space, and the BIOS, with the laptop model as the header.
- Detail windows move to stay on screen when they grow.

### Other

- **Keyboard off with the screen** (optional): the lighting fades out in about half a second as the display turns off, and fades back in to where it was when it turns on.
- **Razer Blade power plan** (optional, `tools/RazerBladePowerPlan.ps1`): Windows' Balanced plan with the energy performance preference at 33 plugged in and 80 on battery and a 5% minimum processor state, for cooler, quieter rest and longer battery. Needs no administrator rights; running it again updates the plan.
- **Experimental:** Max fan on the Blade 15 Base (2020) asks for 10000 RPM instead of 7000. The fans cannot go beyond their own maximum either way; this is being tested and may go back to 7000.

## 1.0.0

The first release. Built and tested on a **Razer Blade 16 (2023)** running Windows 11. Other models are not supported yet.

### What it does

- **Performance modes** (Balanced, Silent, Custom with CPU and GPU boost), with separate profiles for plugged in and on battery that switch automatically when you plug or unplug.
- **Fans:** live CPU and GPU fan speed, and a Max fan speed button.
- **Temperatures:** GPU (from the graphics driver) and CPU (from a sensor in the laptop's controller), read only while the window is open.
- **Battery charge limit** (60%, 80% or 100%) and **display refresh rate** (60 Hz, 120 Hz or Auto, following the power source).
- **Lighting:** keyboard effects and brightness, and the Razer logo on the lid.
- **Razer background software:** shows what is running, and can stop it and turn off its start-at-login entry, then restore everything exactly as it was. Nothing is ever force-closed or uninstalled.
- **Free up GPU:** lists apps keeping the dedicated GPU awake and asks before closing them, on demand or automatically when you unplug (off by default).
- **Open from anywhere:** press **Fn+Del** in any program, even a game, to bring the window to the front. An **Always on top** setting is also available.
- **Readable on high-resolution screens:** the window grows with Windows' display scaling (about 30% larger at 225%), and the size can be set by hand with `WindowScale` in `settings.json`.
- **Settings:** start at login, automatic profile switching, hide when clicking away, and **Reset to defaults**.
- Lives in the tray, needs no account, no cloud, no driver and no background service of its own. It does nothing until you open it.

### Installing

- Requires the **.NET 10 Desktop Runtime**. The installer checks for it and refuses to install until it is present, then points you to the download.
- Installs for the current user only; no administrator prompt. The app asks for administrator approval only when you press Stop on Razer's services.
- The installer and the app are **not code-signed**, so Windows may show a SmartScreen warning ("More info", then "Run anyway") and "Unknown publisher".

### Known limits

- **The CPU die temperature is not shown.** Windows does not expose it without a kernel driver, and RazerHelper installs none. The CPU figure comes from a sensor in the laptop's controller. Compared with MSI Afterburner under load on a Blade 16 (2023) it was very close, but it updates more slowly, so Afterburner's number moves faster.
- **No choice of keyboard color or per-key lighting.** On the Blade 16 the laptop only honors a chosen color in a mode that also turns off the Fn media keys, so RazerHelper offers the built-in effects (including a static Razer green) instead.
- **Games in true exclusive fullscreen** can cover the window, and opening it may make such a game minimize. Borderless and windowed games work.
- **While RazerHelper runs, the Insert key** (Fn+Del on the Blade) opens the window instead of toggling overwrite mode.
- Manual fan curves and the CPU overclock toggle are not included yet.

### Use at your own risk

RazerHelper writes to the laptop's embedded controller and can stop and disable Windows services. It only sends commands that were verified on a Blade 16 (2023), but it is provided as is, without warranty. See the disclaimer in the README.
