using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using RazerHelper.UI.Forms;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>What the Audio page asks of Windows and Equalizer APO; tests and screenshots pass their own.</summary>
internal sealed record AudioBackend(
    Func<IReadOnlyList<AudioDevice>> Devices,
    Func<string?> DefaultDeviceId,
    Action<string> SetDefaultDevice,
    Func<bool> EqualizerInstalled,
    Func<string, bool> EqualizerOnDevice,
    Func<Task<bool>> OpenDeviceSelector,
    Action OpenDownloadPage,
    Action<string> WriteConfig)
{
    public static AudioBackend Windows => new(
        WindowsAudio.OutputDevices,
        WindowsAudio.DefaultOutputId,
        WindowsAudio.SetDefaultOutput,
        () => EqualizerApo.IsInstalled,
        EqualizerApo.IsHookedOn,
        EqualizerApo.OpenDeviceSelectorAsync,
        ExternalLinks.OpenEqualizerApoDownload,
        text =>
        {
            if (EqualizerApo.ConfigFolder() is not { } folder)
                throw new DirectoryNotFoundException("Equalizer APO's config folder is gone.");

            new EqualizerApoWriter(folder).Write(text);
        });
}

/// <summary>What the Audio page keeps: the presets the user saved and each device's equalizer.</summary>
internal sealed record AudioChoices(EqPreset[] Presets, Dictionary<string, AudioDeviceEq> Devices);

/// <summary>
/// The output device and its equalizer. Top: the device Windows plays to;
/// picking another in the list switches Windows to it. Below: the equalizer, with Reset and its on/off switch
/// beside its title, its preset with Save, New and Delete, the ten bands with
/// their curve, then Bass enhancer, Dynamic boost and Clarity, ten levels each, added to
/// whatever curve plays. At the bottom, only while it is needed, why the
/// equalizer cannot play yet: Equalizer APO is missing (with its download),
/// or not hooked into this device (with its Device Selector).
/// </summary>
/// <remarks>
/// Each device keeps its own curve and boosts, which play as soon as they
/// change: Equalizer APO reads its file again by itself. There is no preamp
/// to set: the app lowers the volume by as much as the curve and boosts lift
/// the loudest frequency, so nothing clips. A preset is where a curve starts;
/// changing a band leaves the preset named until Save keeps the change in it
/// (the app's own presets are never changed: Save asks for a new name instead).
/// </remarks>
internal sealed class AudioPage : PageView
{
    private static int RowHeight => S(40);
    private static int BoostRowHeight => S(36);

    private readonly AudioBackend _backend;
    private readonly List<EqPreset> _savedPresets;
    private readonly Dictionary<string, AudioDeviceEq> _devices;
    private readonly ThemedToolTip _toolTip = new();

    private IReadOnlyList<AudioDevice> _shownDevices = [];
    private string? _deviceId;

    private readonly DropdownButton _deviceList;
    private readonly ToggleSwitch _enabled;
    private readonly DropdownButton _presetList;
    private readonly Button _save;
    private readonly Button _new;
    private readonly Button _delete;
    private readonly EqualizerGraph _graph;
    private readonly ThemedSlider _bassBoost;
    private readonly ThemedSlider _dynamicBoost;
    private readonly ThemedSlider _clarityBoost;
    private readonly Label _bassBoostValue;
    private readonly Label _dynamicBoostValue;
    private readonly Label _clarityBoostValue;
    private readonly Panel _notice;
    private readonly Label _noticeText;
    private readonly RoundedButton _noticeButton;

    // Writes wait a moment after the last change, so a drag writes the file a few times, not on every step.
    private readonly System.Windows.Forms.Timer _writeDelay = new() { Interval = 150 };
    private bool _showing;

    public AudioPage(AudioBackend backend, AudioChoices choices)
    {
        _backend = backend;
        _savedPresets = [.. choices.Presets.Where(preset => !EqPreset.IsBuiltIn(preset.Name))];
        _devices = new Dictionary<string, AudioDeviceEq>(choices.Devices, StringComparer.OrdinalIgnoreCase);

        // Output device.
        var first = HeaderRow("Output device");
        first.Margin = new Padding(0, S(18), 0, 0);
        Add(first);

        _deviceList = new DropdownButton([], L.T("No output device"))
        {
            Font = SemiBoldTitleFont(16)
        };

        Add(Row(RowHeight, (_deviceList, Fill: true)));

        var divider = CreateDivider();
        divider.Margin = new Padding(0, S(22), 0, S(20));
        Add(divider);

        // Equalizer: Reset, in grey that turns green under the pointer, and the switch, beside the title.
        _enabled = new ToggleSwitch { AccessibleName = L.T("Equalizer"), Margin = Padding.Empty };

        var reset = new Label
        {
            AutoSize = true,
            Cursor = Cursors.Hand,
            Font = SemiBoldFont(13),
            ForeColor = SubtleTextColor,
            Margin = new Padding(0, S(4), S(16), 0),
            Text = L.T("Reset")
        };
        reset.MouseEnter += (_, _) => reset.ForeColor = RazerGreen;
        reset.MouseLeave += (_, _) => reset.ForeColor = SubtleTextColor;
        _toolTip.SetToolTip(reset, L.T("Back to flat, with the boosts off"));

        var beside = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            WrapContents = false
        };
        beside.Controls.Add(reset);
        beside.Controls.Add(_enabled);
        Add(HeaderRow("Equalizer", beside));

        _presetList = new DropdownButton([]) { Font = SemiBoldTitleFont(16), Width = S(228) };
        _save = SizedButton("Save");
        _new = SizedButton("New");
        _delete = SizedButton("Delete");
        Add(Row(RowHeight, (_presetList, Fill: false), (_save, Fill: false), (_new, Fill: false), (_delete, Fill: false)));

        _graph = new EqualizerGraph
        {
            Margin = new Padding(0, S(16), 0, 0),
            Size = new Size(ContentWidth, S(190))
        };
        Add(_graph);

        // The boosts, under the curve.
        (var bassRow, _bassBoost, _bassBoostValue) = BoostRow("Bass enhancer");
        bassRow.Margin = new Padding(0, S(14), 0, 0);
        Add(bassRow);

        (var dynamicRow, _dynamicBoost, _dynamicBoostValue) = BoostRow("Dynamic boost");
        dynamicRow.Margin = new Padding(0, S(6), 0, 0);
        Add(dynamicRow);

        (var clarityRow, _clarityBoost, _clarityBoostValue) = BoostRow("Clarity");
        clarityRow.Margin = new Padding(0, S(6), 0, 0);
        Add(clarityRow);

        // Why the equalizer cannot play yet, when it cannot.
        (_notice, _noticeText, _noticeButton) = CreateNotice();
        AddWide(_notice);

        // Events.
        _deviceList.SelectionChanged += (_, _) =>
        {
            if (_deviceList.SelectedIndex >= 0)
            {
                var id = _shownDevices[_deviceList.SelectedIndex].Id;
                MakeDefault(id);
                ShowDevice(id);
            }
        };

        _enabled.CheckedChanged += (_, _) =>
        {
            _graph.Active = _enabled.Checked;
            _bassBoost.Available = _enabled.Checked;
            _dynamicBoost.Available = _enabled.Checked;
            _clarityBoost.Available = _enabled.Checked;

            if (!_showing)
                Change(device => device with { Enabled = _enabled.Checked });
        };

        _presetList.SelectionChanged += (_, _) =>
        {
            if (Presets.ElementAtOrDefault(_presetList.SelectedIndex) is { } preset)
            {
                Change(device => device.WithPreset(preset));
                ShowCurve();
            }
        };
        _save.Click += (_, _) => SavePreset();
        _new.Click += (_, _) => SaveAsNew();
        _delete.Click += (_, _) => DeletePreset();

        _graph.GainsChanged += (_, _) => Change(device => device with { Gains = [.. _graph.Gains] });

        _bassBoost.ValueChanged += (_, _) =>
        {
            _bassBoostValue.Text = $"{_bassBoost.Value}";

            if (!_showing)
                Change(device => device with { BassBoost = _bassBoost.Value });
        };

        _dynamicBoost.ValueChanged += (_, _) =>
        {
            _dynamicBoostValue.Text = $"{_dynamicBoost.Value}";

            if (!_showing)
                Change(device => device with { DynamicBoost = _dynamicBoost.Value });
        };

        _clarityBoost.ValueChanged += (_, _) =>
        {
            _clarityBoostValue.Text = $"{_clarityBoost.Value}";

            if (!_showing)
                Change(device => device with { ClarityBoost = _clarityBoost.Value });
        };

        reset.Click += (_, _) =>
        {
            Change(device => device.WithPreset(EqPreset.BuiltIn[0]) with { BassBoost = 0, DynamicBoost = 0, ClarityBoost = 0 });
            ShowCurve();
        };

        _writeDelay.Tick += (_, _) =>
        {
            _writeDelay.Stop();
            Commit();
        };

        RefreshPresetList();
    }

    /// <summary>Raised with everything the page keeps whenever it changes; the window saves it.</summary>
    public event EventHandler<AudioChoices>? ChoicesChanged;

    /// <summary>The presets offered: the app's, then the user's.</summary>
    private IReadOnlyList<EqPreset> Presets => [.. EqPreset.BuiltIn, .. _savedPresets];

    public override void OnPageShown() => ReadDevices();

    public override void OnPageHidden()
    {
        // A change still waiting is written now, not lost.
        if (_writeDelay.Enabled)
        {
            _writeDelay.Stop();
            Commit();
        }
    }

    // The devices plugged in now, keeping the one shown if it still is,
    // else Windows' default.
    private void ReadDevices()
    {
        _shownDevices = _backend.Devices();
        _deviceList.Replace([.. _shownDevices.Select(device => device.Name)]);

        var defaultId = _backend.DefaultDeviceId();
        var keep = _shownDevices.FirstOrDefault(device => Same(device.Id, defaultId))
                   ?? _shownDevices.FirstOrDefault(device => Same(device.Id, _deviceId))
                   ?? _shownDevices.FirstOrDefault();

        if (keep is null)
        {
            _deviceId = null;
            ShowState();
            return;
        }

        ShowDevice(keep.Id);
    }

    private void ShowDevice(string id)
    {
        _deviceId = id;
        _deviceList.Select(_shownDevices.ToList().FindIndex(device => Same(device.Id, id)));
        ShowCurve();
        ShowState();
    }

    // The device's equalizer, as saved.
    private void ShowCurve()
    {
        var device = Current;

        _showing = true;
        _enabled.Checked = device.Enabled;
        _graph.Show(device.Gains);
        _graph.Active = device.Enabled;
        _bassBoost.Value = device.BassBoost;
        _bassBoostValue.Text = $"{device.BassBoost}";
        _dynamicBoost.Value = device.DynamicBoost;
        _dynamicBoostValue.Text = $"{device.DynamicBoost}";
        _bassBoost.Available = device.Enabled;
        _dynamicBoost.Available = device.Enabled;
        _clarityBoost.Value = device.ClarityBoost;
        _clarityBoostValue.Text = $"{device.ClarityBoost}";
        _clarityBoost.Available = device.Enabled;
        _showing = false;

        RefreshPresetList();
    }

    // What can be done for the device: making it the default, the preset
    // buttons, and why the equalizer cannot play yet.
    private void ShowState()
    {
        var hasDevice = _deviceId is not null;
        var installed = _backend.EqualizerInstalled();
        var hooked = installed && hasDevice && _backend.EqualizerOnDevice(_deviceId!);

        _notice.Visible = !hooked;
        _notice.Tag = installed ? "selector" : "download";
        _noticeText.ForeColor = SubtleTextColor;
        _noticeText.Text = !installed
            ? L.T("The equalizer needs Equalizer APO.")
            : hasDevice
                ? L.T("Equalizer APO isn't turned on for this device yet.")
                : L.T("No output device is plugged in.");
        _noticeButton.Text = L.T(installed ? "Turn on for this device" : "Download Equalizer APO");
        _noticeButton.Visible = installed ? hasDevice : true;
        LayoutNotice();
    }

    private AudioDeviceEq Current =>
        _deviceId is not null && _devices.TryGetValue(_deviceId, out var saved) ? saved.Normalized() : AudioDeviceEq.Default;

    // Changes the shown device's equalizer, saves it, and writes it out shortly.
    private void Change(Func<AudioDeviceEq, AudioDeviceEq> change)
    {
        if (_deviceId is null || _showing)
            return;

        var before = Current;
        var after = change(before);

        // A curve that no longer matches its preset keeps the name, so Save knows where it came from.
        _devices[_deviceId] = after;
        RefreshPresetButtons();

        _writeDelay.Stop();
        _writeDelay.Start();
    }

    private void Commit()
    {
        ChoicesChanged?.Invoke(this, new AudioChoices([.. _savedPresets], new Dictionary<string, AudioDeviceEq>(_devices)));

        if (!_backend.EqualizerInstalled())
            return;

        try
        {
            _backend.WriteConfig(EqualizerApoConfig.Build(_devices));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Could not write Equalizer APO's config.", exception);
            _notice.Visible = true;
            _noticeText.ForeColor = Color.IndianRed;
            _noticeText.Text = L.T("Could not save the equalizer to Equalizer APO.");
            _noticeButton.Visible = false;
            LayoutNotice();
        }
    }

    private void RefreshPresetList()
    {
        var presets = Presets;
        _presetList.Replace([.. presets.Select(preset => EqPreset.IsBuiltIn(preset.Name) ? L.T(preset.Name) : preset.Name)]);
        _presetList.Select(IndexOfPreset(Current.Preset));
        RefreshPresetButtons();
    }

    private int IndexOfPreset(string? name) =>
        name is null ? -1 : Presets.ToList().FindIndex(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

    // Save keeps a change in the user's own preset; Delete removes one. The app's own stay as they are.
    private void RefreshPresetButtons()
    {
        var current = Current;
        var preset = Presets.ElementAtOrDefault(IndexOfPreset(current.Preset));
        var own = preset is not null && !EqPreset.IsBuiltIn(preset.Name);

        SetAvailability(_delete, own, _toolTip, "The app's own presets can't be deleted.");
        SetAvailability(_save, preset is null || !preset.Matches(current), _toolTip, "No changes to save.");
    }

    private void SavePreset()
    {
        if (_deviceId is null || _save.Cursor != Cursors.Hand)
            return;

        var current = Current;

        if (current.Preset is { } name && !EqPreset.IsBuiltIn(name) && IndexOfPreset(name) >= 0)
        {
            Keep(new EqPreset(name, [.. current.Gains]));
            return;
        }

        SaveAsNew();
    }

    private void SaveAsNew()
    {
        if (_deviceId is null)
            return;

        using var hold = KeepOpen();
        using var ask = new PresetNameForm(Current.Preset is { } name && !EqPreset.IsBuiltIn(name) ? name : L.T("My preset"));

        if (ask.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        var current = Current;
        Keep(new EqPreset(ask.PresetName, [.. current.Gains]));
    }

    // Adds or replaces one of the user's presets and makes it the device's.
    private void Keep(EqPreset preset)
    {
        _savedPresets.RemoveAll(each => string.Equals(each.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        _savedPresets.Add(preset);
        _devices[_deviceId!] = Current with { Preset = preset.Name };

        RefreshPresetList();
        Commit();
    }

    private void DeletePreset()
    {
        if (_deviceId is null || _delete.Cursor != Cursors.Hand || Current.Preset is not { } name)
            return;

        _savedPresets.RemoveAll(each => string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase));

        // Any device that started from it keeps its curve, with no preset named.
        foreach (var (id, device) in _devices.ToList())
        {
            if (string.Equals(device.Preset, name, StringComparison.OrdinalIgnoreCase))
                _devices[id] = device with { Preset = null };
        }

        RefreshPresetList();
        Commit();
    }

    // The device picked becomes the one Windows plays to.
    private void MakeDefault(string id)
    {
        try
        {
            _backend.SetDefaultDevice(id);
        }
        catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or InvalidCastException)
        {
            AppLog.Error("Could not change the default audio output.", exception);
        }
    }

    private async Task NoticeClickedAsync()
    {
        if (!Equals(_notice.Tag, "selector"))
        {
            _backend.OpenDownloadPage();
            return;
        }

        using (KeepOpen())
            await _backend.OpenDeviceSelector();

        ShowState();

        // A device just hooked in gets its curve at once.
        if (_deviceId is not null && !_notice.Visible)
            Commit();
    }

    private static bool Same(string? one, string? other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    // A section title with an optional control at the right, and the gap below it.
    private static Panel HeaderRow(string title, Control? right = null)
    {
        var row = new Panel
        {
            BackColor = BackgroundColor,
            Margin = Padding.Empty,
            Size = new Size(ContentWidth, SectionHeaderHeight)
        };

        var label = CreateSectionLabel(title);
        label.Dock = DockStyle.None;
        label.Location = Point.Empty;
        label.Height = S(24);
        row.Controls.Add(label);

        if (right is not null)
        {
            row.Controls.Add(right);
            right.Location = new Point(ContentWidth - right.PreferredSize.Width, (S(24) - right.PreferredSize.Height) / 2);
            right.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }

        return row;
    }

    // Controls side by side, 12px apart: each its own width, except the one
    // that fills what the others leave.
    private static TableLayoutPanel Row(int height, params (Control Control, bool Fill)[] parts)
    {
        var row = new TableLayoutPanel
        {
            BackColor = BackgroundColor,
            ColumnCount = parts.Length,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1,
            Size = new Size(ContentWidth, height)
        };

        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < parts.Length; index++)
        {
            var (control, fill) = parts[index];

            row.ColumnStyles.Add(fill
                ? new ColumnStyle(SizeType.Percent, 100F)
                : new ColumnStyle(SizeType.Absolute, control.Width + (index < parts.Length - 1 ? ButtonGap : 0)));

            control.Margin = new Padding(0, 0, index < parts.Length - 1 ? ButtonGap : 0, 0);
            control.Dock = control is ThemedSlider ? DockStyle.None : DockStyle.Fill;
            row.Controls.Add(control, index, 0);
        }

        // With none to fill, an empty last column takes the rest, so the last control keeps its width.
        if (!parts.Any(part => part.Fill))
        {
            row.ColumnCount++;
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        }

        // The slider sits on the row's middle rather than filling it.
        foreach (var slider in parts.Select(part => part.Control).OfType<ThemedSlider>())
        {
            slider.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            slider.Margin = new Padding(0, 0, ButtonGap, 0);
        }

        return row;
    }

    // A boost's row: its name, its slider from 0 (off) to 10, and the level at the right.
    private static (TableLayoutPanel Row, ThemedSlider Slider, Label Value) BoostRow(string text)
    {
        var name = new Label
        {
            AutoSize = false,
            Font = DesignFont(15),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            Size = new Size(S(156), BoostRowHeight),
            Text = L.T(text),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var slider = new ThemedSlider(0, EqBoosts.Levels, 1) { AccessibleName = L.T(text) };

        var value = new Label
        {
            AutoSize = false,
            Font = DesignFont(16, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            Size = new Size(S(32), BoostRowHeight),
            Text = "0",
            TextAlign = ContentAlignment.MiddleRight
        };

        return (Row(BoostRowHeight, (name, Fill: false), (slider, Fill: true), (value, Fill: false)), slider, value);
    }

    // A button as wide as its text needs, 16px each side.
    private static Button SizedButton(string text)
    {
        var button = CreateActionButton(text);
        button.Font = SemiBoldTitleFont(16);
        button.Dock = DockStyle.None;
        button.Size = new Size(TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.NoPadding).Width + S(32), RowHeight);
        return button;
    }

    // The notice: an "i" in a circle, the reason in grey, and the green
    // button at the right with its glow, which the row paints.
    private (Panel Row, Label Text, RoundedButton Button) CreateNotice()
    {
        var room = GlowRoom;

        var row = new Panel
        {
            BackColor = BackgroundColor,
            Margin = new Padding(0, S(14) - room, 0, 0),
            Size = new Size(WideWidth, RowHeight + 2 * room)
        };
        Glow.Attach(row);

        var icon = new InfoIcon { Location = new Point(room, room + (RowHeight - S(20)) / 2), Size = new Size(S(20), S(20)) };

        var text = new Label
        {
            AutoEllipsis = false,
            AutoSize = false,
            BackColor = BackgroundColor,
            Font = DesignFont(14),
            ForeColor = SubtleTextColor,
            Margin = Padding.Empty,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var button = new RoundedButton
        {
            BackColor = RazerGreen,
            Cursor = Cursors.Hand,
            Font = SemiBoldTitleFont(16),
            ForeColor = OnGreenTextColor,
            GlowRoom = room,
            Margin = Padding.Empty
        };
        button.Click += async (_, _) => await NoticeClickedAsync();

        row.Controls.Add(icon);
        row.Controls.Add(text);
        row.Controls.Add(button);
        return (row, text, button);
    }

    // The button sized to its text at the right; the text fills the rest.
    private void LayoutNotice()
    {
        var room = GlowRoom;
        var width = _noticeButton.Visible
            ? TextRenderer.MeasureText(_noticeButton.Text, _noticeButton.Font, Size.Empty, TextFormatFlags.NoPadding).Width + S(32)
            : 0;

        _noticeButton.Bounds = new Rectangle(WideWidth - room - width - room, 0, width + 2 * room, RowHeight + 2 * room);

        var left = room + S(20) + S(12);
        var right = _noticeButton.Visible ? _noticeButton.Left + room - S(12) : WideWidth - room;
        _noticeText.Bounds = new Rectangle(left, room, Math.Max(0, right - left), RowHeight);
        _notice.Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _writeDelay.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    // A small "i" in a grey circle.
    private sealed class InfoIcon : Control
    {
        public InfoIcon()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = BackgroundColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(BackColor);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var stroke = S(1.5f);
            using var pen = new Pen(SubtleTextColor, stroke);
            graphics.DrawEllipse(pen, stroke / 2, stroke / 2, Width - stroke - 1, Height - stroke - 1);

            using var brush = new SolidBrush(SubtleTextColor);
            var middle = Width / 2f;
            graphics.FillEllipse(brush, middle - S(1.2f), Height * 0.25f, S(2.4f), S(2.4f));
            graphics.FillRectangle(brush, middle - S(1f), Height * 0.42f, S(2f), Height * 0.34f);
        }
    }
}
