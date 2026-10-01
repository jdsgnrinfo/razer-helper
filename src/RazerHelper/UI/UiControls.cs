using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

internal static class UiControls
{
    public static TableLayoutPanel CreateTwoColumnLayout(float leftWidth, float rightWidth)
    {
        var layout = new TableLayoutPanel
        {
            BackColor = CardColor,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, leftWidth));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, rightWidth));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return layout;
    }

    /// <summary>A section's title row: the 16px uppercase title, then the 12px gap before what the section shows.</summary>
    public static int SectionHeaderHeight => S(24 + 12);

    /// <summary>
    /// The text of the performance mode and display buttons: a step bolder
    /// than the other buttons' and 1pt larger (1pt is 1.33px).
    /// </summary>
    public static Font ProfileButtonFont => SemiBoldTitleFont(16 + 1 / 0.75F);

    /// <summary>The space between two buttons side by side.</summary>
    public static int ButtonGap => S(12);

    /// <summary>A title row, <see cref="SectionHeaderHeight"/> tall and docked to the top, with room below for its gap.</summary>
    public static TableLayoutPanel CreateHeaderLayout(float leftWidth = 60F, float rightWidth = 40F)
    {
        var header = CreateTwoColumnLayout(leftWidth, rightWidth);
        header.Dock = DockStyle.Top;
        header.Height = SectionHeaderHeight;
        header.Padding = new Padding(0, 0, 0, S(12));
        return header;
    }

    public static Control CreateSectionHeader(string title, string detail, Glyph? icon = null)
    {
        var header = CreateHeaderLayout();

        header.Controls.Add(CreateSectionLabel(title, icon), 0, 0);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            var label = CreateHeaderValueLabel();
            label.Text = L.T(detail);
            header.Controls.Add(label, 1, 0);
        }

        return header;
    }

    /// <summary>A value at the right of a title row, such as the display's mode: 16px bold white.</summary>
    public static Label CreateHeaderValueLabel() => new()
    {
        AutoSize = true,
        BackColor = CardColor,
        Dock = DockStyle.Right,
        Font = TitleFont(16, FontStyle.Bold),
        ForeColor = Color.White,
        Margin = Padding.Empty,
        TextAlign = ContentAlignment.MiddleRight
    };

    /// <summary>
    /// A small button sized to its text, such as "More info" or the footer's: 14px semi-bold text,
    /// 10px each side, 30px tall.
    /// </summary>
    public static Button CreateSmallButton(string text)
    {
        var button = CreateActionButton(text);
        var font = button.Font = SemiBoldTitleFont(14);
        var textSize = TextRenderer.MeasureText(button.Text, font, Size.Empty, TextFormatFlags.NoPadding);

        button.Dock = DockStyle.None;
        button.Margin = Padding.Empty;
        button.Size = new Size(textSize.Width + S(2 * 10), S(30));
        return button;
    }

    /// <param name="inset">Space before the icon (or the text, without one), for titles that line up with an indented row.</param>
    public static Label CreateSectionLabel(string text, Glyph? icon = null, int inset = 0) => new SectionLabel(icon, inset)
    {
        AutoSize = true,
        BackColor = CardColor,
        Dock = DockStyle.Left,
        Font = TitleFont(16, FontStyle.Bold),
        Margin = Padding.Empty,
        ForeColor = Color.White,
        // Titles are in capitals, as in the design.
        Text = L.T(text).ToUpper(System.Globalization.CultureInfo.CurrentUICulture),
        TextAlign = ContentAlignment.MiddleLeft
    };

    /// <summary>
    /// A section title with an optional icon before it, in the text color.
    /// The icon sits in the label's left padding, so auto-sizing leaves room
    /// for it and the text lines up exactly as without one.
    /// </summary>
    private sealed class SectionLabel : Label
    {
        private static int IconSize => S(18);
        private static int IconGap => S(10);

        private readonly Glyph? _icon;
        private readonly int _inset;

        public SectionLabel(Glyph? icon, int inset = 0)
        {
            _icon = icon;
            _inset = inset;

            Padding = new Padding(inset + (icon is null ? 0 : IconSize + IconGap), 0, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_icon is { } icon)
                Glyphs.Draw(e.Graphics, icon, new RectangleF(_inset, (Height - IconSize) / 2f, IconSize, IconSize), ForeColor);
        }
    }

    /// <param name="slots">How many button widths the row is divided into; more than the buttons leaves the extra ones empty at the right, so rows of different lengths line up.</param>
    public static Control CreateButtonGrid(IReadOnlyList<string> buttonNames, string nameSuffix, int slots = 0)
    {
        var count = Math.Max(slots, buttonNames.Count);
        var grid = new TableLayoutPanel
        {
            BackColor = CardColor,
            ColumnCount = count,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < count; index++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / count));

        for (var index = 0; index < buttonNames.Count; index++)
        {

            var name = buttonNames[index];
            var button = CreateActionButton(name);

            // ButtonGap between neighbours and none at the outer edges, split so
            // every button comes out the same width.
            button.Margin = new Padding(ButtonGap * index / count, 0, ButtonGap * (count - 1 - index) / count, 0);
            button.Name = $"{name}{nameSuffix}";
            button.Tag = name;
            grid.Controls.Add(button, index, 0);
        }

        return grid;
    }

    /// <summary>Highlights <paramref name="selected"/> and resets the rest; null clears the selection.</summary>
    public static void HighlightSelected(IEnumerable<Button> buttons, Button? selected)
    {
        foreach (var button in buttons)
        {
            var isSelected = ReferenceEquals(button, selected);

            button.BackColor = isSelected ? RazerGreen : ButtonColor;
            button.ForeColor = isSelected ? OnGreenTextColor : Color.White;
        }
    }

    /// <summary>
    /// Draws a button as unavailable and says why on hover, or restores it.
    /// The button stays enabled underneath, because WinForms shows no tooltip
    /// on a disabled control; so its click handler must check availability
    /// itself. An unavailable button also stays out of the keyboard focus order.
    /// </summary>
    public static void SetAvailability(Button button, bool available, ToolTip toolTip, string reasonWhenUnavailable)
    {
        // A selected button is green with dark text; the rest are dark with light text.
        var normalText = button.BackColor == RazerGreen ? OnGreenTextColor : Color.White;

        button.ForeColor = available ? normalText : SystemColors.GrayText;
        button.Cursor = available ? Cursors.Hand : Cursors.Default;
        button.TabStop = available;
        toolTip.SetToolTip(button, available ? string.Empty : L.T(reasonWhenUnavailable));
    }

    public static Button CreateActionButton(string text)
    {
        return new RoundedButton
        {
            BackColor = ButtonColor,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Font = TitleFont(16),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            Text = L.T(text)
        };
    }
}
