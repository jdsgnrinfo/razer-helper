using RazerHelper.Helpers;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The tray icon's right-click menu in the app's dark style: a dark rounded
/// panel with a thin border (from Windows 11, like the windows), white text,
/// a highlight across the whole width under the pointer and thin separators. No
/// image column, so the text starts close to the edge.
/// </summary>
internal sealed class TrayMenu : ContextMenuStrip
{
    private static int ListInset => S(4);
    private static readonly Color HoverColor = Color.FromArgb(0x33, 0x33, 0x33);

    public TrayMenu()
    {
        BackColor = ButtonColor;
        ForeColor = Color.White;
        Font = DesignFont(12);
        Padding = new Padding(0, ListInset, 0, ListInset);
        Renderer = new DarkRenderer();
        ShowCheckMargin = false;
        ShowImageMargin = false;
    }

    /// <summary>
    /// Adds an item with the menu's spacing and colors. The menu makes every
    /// item as wide as the widest, so the highlight reaches both edges.
    /// </summary>
    public ToolStripMenuItem AddItem(string text, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text, null, onClick)
        {
            ForeColor = Color.White,
            Padding = S(new Padding(8, 7, 24, 7))
        };

        Items.Add(item);
        return item;
    }

    public void AddSeparator() => Items.Add(new ToolStripSeparator { Margin = new Padding(0, S(3), 0, S(3)) });

    // Rounded corners and the thin border, as the app's windows have.
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.Apply(Handle, BorderColor);
    }

    private sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors())
        {
            RoundedEdges = false;
        }

        // The border comes from Windows, with the rounded corners.
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) =>
            e.Graphics.Clear(ButtonColor);

        // Under the pointer: a fill across the whole width of the menu.
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;

            using var fill = new SolidBrush(HoverColor);
            e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item;
            var left = S(12);

            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                new Rectangle(left, 0, item.Width - left, item.Height),
                item.Enabled ? Color.White : SubtleTextColor,
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // A thin line, kept in from the sides.
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;

            using var line = new Pen(BorderColor);
            e.Graphics.DrawLine(line, S(8), y, e.Item.Width - S(8), y);
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ButtonColor;
        public override Color MenuBorder => ButtonColor;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => HoverColor;
        public override Color ImageMarginGradientBegin => ButtonColor;
        public override Color ImageMarginGradientMiddle => ButtonColor;
        public override Color ImageMarginGradientEnd => ButtonColor;
        public override Color SeparatorDark => BorderColor;
        public override Color SeparatorLight => ButtonColor;
    }
}
