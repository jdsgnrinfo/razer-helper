using System.ComponentModel;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// A dark drop-down list. The stock ComboBox ignores the theme (its arrow and
/// list stay light), so this is a flat button that opens a dark menu under
/// itself, in the same style as the other buttons.
/// </summary>
/// <remarks>
/// <see cref="SelectionChanged"/> fires only when the user picks an item;
/// <see cref="Select"/> changes what is shown without raising it, so showing
/// what the laptop is doing never looks like a click.
/// </remarks>
internal sealed class DropdownButton : RoundedButton
{
    private static readonly Color HoverColor = ButtonHoverColor;
    private static readonly SolidBrush ArrowBrush = new(Color.White);
    private static readonly SolidBrush DisabledArrowBrush = new(SystemColors.GrayText);

    private string[] _items;
    private readonly string _placeholder;
    // A plain drop-down, not a menu: a menu keeps margins for check marks,
    // icons and shortcut keys, which left a gap beside the hover highlight.
    private readonly ToolStripDropDown _menu = new() { LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow };
    private int _selectedIndex = -1;

    // Optional color swatches, one per item (null for an item without one).
    // Shown beside each item in the list and beside the choice on the button.
    private readonly Color?[]? _swatches;

    public DropdownButton(IReadOnlyList<string> items, string placeholder = "-", IReadOnlyList<Color?>? swatches = null)
    {
        _items = [.. items];
        _placeholder = placeholder;
        _swatches = swatches is null ? null : [.. swatches];

        BackColor = ButtonColor;
        Cursor = Cursors.Hand;
        Font = SemiBoldTitleFont(16);
        ForeColor = Color.White;
        Padding = S(new Padding(_swatches is null ? 12 : 12 + 16 + 8, 0, 12 + 16 + 8, 0));
        TextAlign = ContentAlignment.MiddleLeft;

        _menu.BackColor = ButtonColor;
        _menu.ForeColor = Color.White;
        _menu.Font = Font;
        _menu.AutoSize = false; // Sized to the button each time it opens.
        _menu.Renderer = new DarkMenuRenderer();
        _menu.Padding = S(new Padding(ListInset));

        // The open list gets the same 4px rounded corners as the buttons, and
        // no outline. Windows 11 draws the rounding itself (smooth, and the
        // "small" preference is 4px); on Windows 10 the list stays square.
        _menu.HandleCreated += (_, _) => RoundedWindow.Apply(_menu.Handle);

        AddItems();
        UpdateText();
    }

    /// <summary>Puts a new list in place of the items (a list without swatches), with none picked.</summary>
    public void Replace(IReadOnlyList<string> items)
    {
        System.Diagnostics.Debug.Assert(_swatches is null, "A list with swatches keeps its items.");

        _items = [.. items];

        foreach (var old in _menu.Items.Cast<ToolStripItem>().ToList())
            old.Dispose();

        _menu.Items.Clear();
        AddItems();
        Select(-1);
    }

    private void AddItems()
    {
        for (var index = 0; index < _items.Length; index++)
        {
            var itemIndex = index;
            var item = new ToolStripMenuItem(_items[index])
            {
                BackColor = ButtonColor,
                ForeColor = Color.White,
                Padding = S(new Padding(4, 4, 4, 4))
            };

            item.Click += (_, _) => Pick(itemIndex);
            _menu.Items.Add(item);

            // The renderer draws the swatch beside the text.
            item.Tag = _swatches?[index];
        }
    }

    private static int SwatchSize => S(16);

    // Base-design pixels: the gap between the list's edge and the items, so the
    // hover highlight never touches the rounded corners, and that highlight's
    // own corner radius.
    private const int ListInset = 2;
    private const int HighlightRadius = 2;

    /// <summary>Raised when the user picks an item.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The picked item, or -1 when none is shown.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex => _selectedIndex;

    /// <summary>Shows an item (or none, with -1) without raising <see cref="SelectionChanged"/>.</summary>
    public void Select(int index)
    {
        _selectedIndex = index >= 0 && index < _items.Length ? index : -1;
        UpdateText();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        // The list is exactly as wide as the button (or as its longest item),
        // and every item fills its full inner width, so the hover highlight
        // runs edge to edge. Left to itself, WinForms sizes the list to its
        // text and keeps room on the right for shortcut keys this list lacks.
        var items = _menu.Items.Cast<ToolStripItem>().ToList();

        foreach (var item in items)
            item.AutoSize = true;

        var itemWidth = Math.Max(Width - _menu.Padding.Horizontal, items.Max(ItemContentWidth));
        var listHeight = _menu.Padding.Vertical;

        foreach (var item in items)
        {
            var height = item.GetPreferredSize(Size.Empty).Height;
            item.AutoSize = false;
            item.Size = new Size(itemWidth, height);
            listHeight += height;
        }

        _menu.Size = new Size(itemWidth + _menu.Padding.Horizontal, listHeight);
        _menu.Show(this, new Point(0, Height));
    }

    // What an item needs: its swatch and the gap after it, its text, and the inset each side.
    private int ItemContentWidth(ToolStripItem item) =>
        (item.Tag is Color ? SwatchSize + SwatchGap : 0) +
        TextRenderer.MeasureText(item.Text, _menu.Font, Size.Empty, TextFormatFlags.NoPadding).Width +
        2 * ItemInset;

    private static int SwatchGap => S(8);

    // Where an item's swatch, or its text without one, starts: as on the button.
    private static int ItemInset => S(12);

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);

        // The arrow, drawn as a small triangle.
        var centerX = Width - S(12 + 8);
        var centerY = Height / 2;

        pevent.Graphics.FillPolygon(Enabled ? ArrowBrush : DisabledArrowBrush,
        [
            new Point(centerX - S(4), centerY - S(2)),
            new Point(centerX + S(4), centerY - S(2)),
            new Point(centerX, centerY + S(3))
        ]);

        // The chosen item's swatch, in the gap the left padding leaves for it.
        if (_selectedIndex >= 0 && _swatches?[_selectedIndex] is { } swatch)
        {
            var box = new Rectangle(S(12), (Height - SwatchSize) / 2, SwatchSize, SwatchSize);

            using var fill = new SolidBrush(swatch);
            pevent.Graphics.FillRectangle(fill, box);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _menu.Dispose();

        base.Dispose(disposing);
    }

    private void Pick(int index)
    {
        if (index == _selectedIndex)
            return;

        Select(index);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateText()
    {
        Text = _selectedIndex >= 0 ? _items[_selectedIndex] : _placeholder;

        // The current choice is picked out in the list, in the app's green.
        for (var index = 0; index < _menu.Items.Count; index++)
            _menu.Items[index].ForeColor = index == _selectedIndex ? RazerGreen : Color.White;
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors())
        {
            RoundedEdges = false;
        }

        // The swatch (if any) and the text, from the left as on the button and
        // centered top to bottom, in the color chosen per item (the selected
        // one is green).
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item;
            var x = ItemInset - S(ListInset); // The item itself starts the list's inset in.

            if (item.Tag is Color swatch)
            {
                var box = new Rectangle(x, (item.Height - SwatchSize) / 2, SwatchSize, SwatchSize);

                using var fill = new SolidBrush(swatch);
                using var border = new Pen(Color.Silver);

                e.Graphics.FillRectangle(fill, box);
                e.Graphics.DrawRectangle(border, box.X, box.Y, box.Width - 1, box.Height - 1);
                x += SwatchSize + SwatchGap;
            }

            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                new Rectangle(x, 0, item.Width - x, item.Height),
                item.ForeColor,
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // No outline around the open list.
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        // The hovered item: a softly rounded fill inside the item, which the
        // list's inset keeps clear of the list's own edges and corners.
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
                return;

            var graphics = e.Graphics;
            var smoothing = graphics.SmoothingMode;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var fill = new SolidBrush(HoverColor);
            using var path = RoundedPath(
                new RectangleF(0, 0, e.Item.Width - 0.5f, e.Item.Height - 0.5f),
                S(HighlightRadius));

            graphics.FillPath(fill, path);
            graphics.SmoothingMode = smoothing;
        }
    }

    private sealed class DarkColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ButtonColor;
        public override Color MenuBorder => ButtonColor;
        public override Color MenuItemBorder => HoverColor;
        public override Color MenuItemSelected => HoverColor;
        public override Color MenuItemSelectedGradientBegin => HoverColor;
        public override Color MenuItemSelectedGradientEnd => HoverColor;
        public override Color ImageMarginGradientBegin => ButtonColor;
        public override Color ImageMarginGradientMiddle => ButtonColor;
        public override Color ImageMarginGradientEnd => ButtonColor;
    }
}
