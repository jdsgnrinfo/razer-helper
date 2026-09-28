# Changelog

## Fork 1.1.0

Changes made in this fork by [jdsgnrinfo](https://github.com/jdsgnrinfo) on top of Paul Rodriguez's [RazerHelper](https://github.com/Paulrod20/razer-helper) 1.0.0. Tested on a **Razer Blade 15 Base (2020)**. Every change can be seen line by line in the [comparison with the original](https://github.com/Paulrod20/razer-helper/compare/main...jdsgnrinfo:razer-helper:main).

### Razer Blade 15 Base (2020)

- **Gaming** performance mode, and **Silent** offered on battery.
- **Max fan** through the model's manual fan method (not in Silent). It is turned off when the app starts and exits, so the fans are never left flat out.
- **Keyboard color:** Static and Breathing in a chosen color. Wave is not offered (single-zone keyboard).
- The charge limit and the max fan flag are shown as unavailable: this firmware only echoes those commands without acting on them.

### Performance modes

- **Tray icon in the mode's color:** green for Balanced, blue for Silent, orange-red for Gaming, purple for Custom, repainted from the logo in the app itself; hovering it shows "RazerHelper (Gaming)".
- **Shortcuts:** Ctrl+Shift+F1, F2 and F3 switch to Balanced, Silent and Gaming from any program, even a game. A small notice at the top right shows the mode's icon, name and status without taking the focus, then fades out.
- **Balanced, Silent, Gaming and Custom on every model.** Gaming used to be offered only on the Blade 15 Base (2020). A laptop whose firmware does not take a mode reports another one back; the app then marks that button as not supported instead of offering it again.

### Language

- Full **Spanish translation**, chosen in Settings; the app restarts in the chosen language. English stays the default.

### Interface

- **Redesign** of every window: flat sections, square-cornered buttons with a thin border, pill sliders, grey and green switches, smaller type and spacing. The main window went from 616x962 to 500x686.
- Every window takes its rounded corners and border from Windows 11.
- **New icons** for the sections, the performance modes and Settings, from open icon sets (see the credits in the README).
- CPU and GPU temperatures in the Performance header, as "CPU: 52°C | GPU: 45°C".
- The charge limit slider shows the chosen limit at its right, like the lighting sliders.

### New windows

- **Custom:** the CPU and GPU boost levels in their own window beside the popup, with live CPU temperature, usage and speed, and GPU temperature, usage, core clock and memory clock. The GPU's usage and core clock come from NVIDIA's library, asked only while the GPU is awake. The window closes by itself if the laptop leaves Custom.
- **Battery details** (More info): power in or out, time left or to full, charge, health against the design capacity, voltage and the battery itself.
- **System information** (System info, in the footer): Windows, CPU, integrated and dedicated GPU, RAM in use, each drive letter with its drive and free space, and the BIOS, with the laptop model as the header.
- Detail windows move to stay on screen when they grow.

### Other

- **Keyboard off with the screen** (optional): the lighting turns off and back on with the display.
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
