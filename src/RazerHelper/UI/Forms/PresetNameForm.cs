using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using RazerHelper.Helpers;
using static RazerHelper.UI.UiControls;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// Asks for the name of an equalizer preset being saved: a dark box with a
/// text field, Save and Cancel. Enter saves, Esc cancels. A name the app's
/// own presets use is refused there and then, so they are never replaced.
/// </summary>
internal sealed class PresetNameForm : Form
{
    private static int ContentWidth => S(320);

    private readonly TextBox _name;
    private readonly Label _problem;

    public PresetNameForm(string suggestion)
    {
        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ForeColor = Color.White;
        Font = GetDesignFont(FontFamilyName, 9F);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        WindowFade.Attach(this);
        Text = "RazerHelper";
        TopMost = true;

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BackgroundColor,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = S(new Padding(20, 18, 20, 18)),
            WrapContents = false
        };

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Font = CapsTitleFont(),
            ForeColor = Color.White,
            Margin = S(new Padding(0, 0, 0, 12)),
            Text = L.T("Save preset").ToUpper(System.Globalization.CultureInfo.CurrentCulture)
        });

        // The field sits on a button-grey panel, which gives it the app's padding and corners' color.
        var field = new Panel
        {
            BackColor = ButtonColor,
            Margin = Padding.Empty,
            Padding = S(new Padding(12, 9, 12, 0)),
            Size = new Size(ContentWidth, S(40))
        };

        _name = new TextBox
        {
            BackColor = ButtonColor,
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            Font = SemiBoldTitleFont(16),
            ForeColor = Color.White,
            MaxLength = 40,
            Text = suggestion
        };
        _name.TextChanged += (_, _) => _problem!.Visible = false;
        field.Controls.Add(_name);
        layout.Controls.Add(field);

        _problem = new Label
        {
            AutoSize = true,
            Font = DesignFont(13),
            ForeColor = Color.IndianRed,
            Margin = S(new Padding(0, 6, 0, 0)),
            MaximumSize = new Size(ContentWidth, 0),
            Visible = false
        };
        layout.Controls.Add(_problem);

        var save = CreateActionButton("Save");
        save.BackColor = RazerGreen;
        save.ForeColor = OnGreenTextColor;
        save.Dock = DockStyle.None;
        save.Font = SemiBoldTitleFont(15);
        save.Size = S(new Size(100, 36));
        save.Click += (_, _) => TrySave();

        var cancel = CreateActionButton("Cancel");
        cancel.Dock = DockStyle.None;
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Font = SemiBoldTitleFont(15);
        cancel.Margin = S(new Padding(0, 0, 12, 0));
        cancel.Size = S(new Size(100, 36));

        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = S(new Padding(0, 16, 0, 0)),
            Width = ContentWidth
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        Shown += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
    }

    /// <summary>The name given, trimmed.</summary>
    public string PresetName => _name.Text.Trim();

    private void TrySave()
    {
        var name = PresetName;

        string? problem = name.Length == 0
            ? L.T("Give the preset a name.")
            : EqPreset.BuiltIn.Any(preset => string.Equals(L.T(preset.Name), name, StringComparison.CurrentCultureIgnoreCase)) || EqPreset.IsBuiltIn(name)
                ? L.T("That name belongs to one of the app's presets. Try another.")
                : null;

        if (problem is not null)
        {
            _problem.Text = problem;
            _problem.Visible = true;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, null);
    }
}
