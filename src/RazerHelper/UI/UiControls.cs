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

    public static Control CreateSectionHeader(string title, string detail, Glyph? icon = null)
    {
        var header = CreateTwoColumnLayout(60F, 40F);
        header.Dock = DockStyle.Top;
        header.Height = S(28);

        header.Controls.Add(CreateSectionLabel(title, icon), 0, 0);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Dock = DockStyle.Right,
                Font = GetDesignFont("Segoe UI", 9.5F),
                ForeColor = Color.Silver,
                Text = L.T(detail),
                TextAlign = ContentAlignment.MiddleRight
            }, 1, 0);
        }

        return header;
    }

    /// <param name="inset">Space before the icon (or the text, without one), for titles that line up with an indented row.</param>
    public static Label CreateSectionLabel(string text, Glyph? icon = null, int inset = 0) => new SectionLabel(icon, inset)
    {
        AutoSize = true,
        Dock = DockStyle.Left,
        Font = GetDesignFont("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = Color.White,
        Text = L.T(text),
        TextAlign = ContentAlignment.MiddleLeft
    };

    /// <summary>
    /// A section title with an optional icon before it, in the text color.
    /// The icon sits in the label's left padding, so auto-sizing leaves room
    /// for it and the text lines up exactly as without one.
    /// </summary>
    private sealed class SectionLabel : Label
    {
        private static int IconSize => S(16);
        private static int IconGap => S(6);

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

    public static Control CreateButtonGrid(IReadOnlyList<string> buttonNames, string nameSuffix)
    {
        var grid = new TableLayoutPanel
        {
            BackColor = CardColor,
            ColumnCount = buttonNames.Count,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        for (var index = 0; index < buttonNames.Count; index++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / buttonNames.Count));

            var name = buttonNames[index];
            var button = CreateActionButton(name);
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
            button.ForeColor = isSelected ? BackgroundColor : Color.White;
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
        var normalText = button.BackColor == RazerGreen ? BackgroundColor : Color.White;

        button.ForeColor = available ? normalText : SystemColors.GrayText;
        button.Cursor = available ? Cursors.Hand : Cursors.Default;
        button.TabStop = available;
        toolTip.SetToolTip(button, available ? string.Empty : L.T(reasonWhenUnavailable));
    }

    /// <summary>
    /// A quiet text link, as in the footer: silver, turning Razer green and
    /// underlined under the pointer.
    /// </summary>
    public static LinkLabel CreateLink(string text)
    {
        var link = new LinkLabel
        {
            ActiveLinkColor = RazerGreen,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Font = GetDesignFont("Segoe UI", 8.5F),
            LinkBehavior = LinkBehavior.HoverUnderline,
            LinkColor = Color.Silver,
            Text = L.T(text)
        };

        link.MouseEnter += (_, _) => link.LinkColor = RazerGreen;
        link.MouseLeave += (_, _) => link.LinkColor = Color.Silver;
        return link;
    }

    public static Button CreateActionButton(string text)
    {
        return new RoundedButton
        {
            BackColor = ButtonColor,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Font = GetDesignFont("Segoe UI", 9.5F),
            ForeColor = Color.White,
            Margin = S(new Padding(4)),
            Text = L.T(text)
        };
    }
}
