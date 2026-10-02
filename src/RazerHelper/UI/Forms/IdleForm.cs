using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The Idle window, opened from the footer: an option to switch Windows'
/// power plan while the laptop is left alone, how many minutes without a key
/// press or mouse move count as idle, and which plan to switch to. Off until
/// the user turns it on; every choice applies and is saved at once.
/// </summary>
internal sealed class IdleForm : Form
{
    /// <summary>The idle times offered, in minutes.</summary>
    public static readonly int[] MinuteChoices = [1, 5, 10, 15, 20, 25, 30];

    private static int ContentWidth => S(512);

    private readonly RadioOption _option;
    private readonly DropdownButton _minutes;
    private readonly DropdownButton _plans;
    private readonly IReadOnlyList<PowerPlan> _planList;
    private Form? _anchor;

    public IdleForm(bool enabled, int minutes, Guid? plan, IReadOnlyList<PowerPlan> plans)
    {
        _planList = plans;

        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont(FontFamilyName, 9F);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true; // Esc closes.
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = L.T("RazerHelper Idle");

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = S(new Padding(24)),
            WrapContents = false
        };

        layout.Controls.Add(WindowTitleRow.Create(this, "Idle", ContentWidth));

        // The on/off choice, marked like the fans' options: ticked, it is on.
        _option = new RadioOption(L.T("Change the power plan when idle"), L.T("Your plan comes back as soon as you use the laptop."))
        {
            Cursor = Cursors.Hand,
            Height = RadioOption.PreferredHeight,
            Margin = new Padding(0, S(12), 0, S(12)),
            Width = ContentWidth
        };
        _option.Click += (_, _) =>
        {
            ShowOption(!IsOn);
            RaiseChanged();
        };
        layout.Controls.Add(_option);

        _minutes = new DropdownButton([.. MinuteChoices.Select(choice => L.F("{0} min", choice))])
        {
            Anchor = AnchorStyles.Right,
            Font = SemiBoldTitleFont(16),
            Size = S(new Size(116, 38))
        };
        _minutes.SelectionChanged += (_, _) => RaiseChanged();

        _plans = new DropdownButton([.. plans.Select(each => each.Name)])
        {
            Anchor = AnchorStyles.Right,
            Font = SemiBoldTitleFont(16),
            Size = S(new Size(200, 38))
        };
        _plans.SelectionChanged += (_, _) => RaiseChanged();

        layout.Controls.Add(SettingsForm.CreateDivider());
        layout.Controls.Add(SettingsForm.CreateCard("Idle for", "Without touching the keyboard or the mouse.", _minutes));
        layout.Controls.Add(SettingsForm.CreateDivider());
        layout.Controls.Add(SettingsForm.CreateCard("Power plan", "Applied once that time has passed.", _plans));

        layout.Controls.Add(new InfoNote(L.T("A video or a game that keeps the screen on counts as using the laptop."), ContentWidth)
        {
            Margin = new Padding(0, S(16), 0, 0)
        });

        WindowOutline.Attach(layout);
        Controls.Add(layout);

        ShowOption(enabled);
        _minutes.Select(Array.IndexOf(MinuteChoices, minutes));
        _plans.Select(plan is { } chosen ? IndexOfPlan(chosen) : -1);
    }

    /// <summary>Raised with every change: on or off, the minutes, and the plan (null while none is picked).</summary>
    public event EventHandler<(bool Enabled, int Minutes, Guid? Plan)>? Changed;

    private bool IsOn => _option.BackColor.ToArgb() == RazerGreen.ToArgb();

    private int IndexOfPlan(Guid plan)
    {
        for (var index = 0; index < _planList.Count; index++)
        {
            if (_planList[index].Id == plan)
                return index;
        }

        return -1;
    }

    // A green BackColor marks it chosen, as on the fans' options.
    private void ShowOption(bool on)
    {
        _option.BackColor = on ? RazerGreen : BackgroundColor;
        _option.Invalidate();
    }

    private void RaiseChanged()
    {
        var minutes = _minutes.SelectedIndex >= 0 ? MinuteChoices[_minutes.SelectedIndex] : MinuteChoices[1];
        Guid? plan = _plans.SelectedIndex >= 0 ? _planList[_plans.SelectedIndex].Id : null;
        Changed?.Invoke(this, (IsOn, minutes, plan));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
            Close();
    }

    /// <summary>Opens next to the popup instead of centered over it, where it would hide it.</summary>
    public void PlaceBeside(Form anchor)
    {
        StartPosition = FormStartPosition.Manual;
        _anchor = anchor;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_anchor is null)
            return;

        // The size is only final once the layout has run.
        PerformLayout();
        var workingArea = Screen.FromRectangle(_anchor.Bounds).WorkingArea;
        Location = WindowPlacement.Beside(_anchor.Bounds, Size, workingArea);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, null); // The outline is drawn by WindowOutline instead.
    }
}
