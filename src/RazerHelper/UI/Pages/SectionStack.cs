using RazerHelper.UI.Sections;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The popup's sections, one under another as they were in the old single
/// window: each row as tall as its section asks, with the gap and the thin
/// line between one section and the next (none after the last). A section
/// that asks for no height takes none, and its line goes with it; one joined
/// to the section above follows it closely, with no line between them.
/// </summary>
internal sealed class SectionStack : TableLayoutPanel
{
    private static int JoinedGap => S(16);

    private readonly List<(Control Section, Func<int> Height, bool Joined)> _rows = [];

    public SectionStack()
    {
        BackColor = BackgroundColor;
        ColumnCount = 1;
        // Room above the first title, so it starts where the other pages' first text does.
        Margin = new Padding(0, S(18), 0, 0);
        Padding = Padding.Empty;
        Width = PageView.ContentWidth;

        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Paint += PaintDividers;
    }

    /// <param name="height">The section's own height, without the gap below it; asked again on every <see cref="Relayout"/>.</param>
    /// <param name="joined">Part of the section above: close under it, with no line between.</param>
    public void AddSection(Control section, Func<int> height, bool joined = false)
    {
        _rows.Add((section, height, joined));
        RowCount = _rows.Count;
        RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        Controls.Add(section, 0, _rows.Count - 1);
        Relayout();
    }

    /// <summary>Sizes every row again, after a section changed height.</summary>
    public void Relayout()
    {
        var total = 0;

        for (var index = 0; index < _rows.Count; index++)
        {
            var (section, height, _) = _rows[index];
            var own = height();
            var gap = own > 0 && NextShown(index) is { } next ? (_rows[next].Joined ? JoinedGap : SectionPanel.GapBelow) : 0;

            section.Margin = new Padding(0, 0, 0, gap);
            RowStyles[index].Height = own + gap;
            total += own + gap;
        }

        Height = total;
        Invalidate();
    }

    // The next row that takes any height, if there is one.
    private int? NextShown(int index)
    {
        for (var next = index + 1; next < _rows.Count; next++)
        {
            if (_rows[next].Height() > 0)
                return next;
        }

        return null;
    }

    private void PaintDividers(object? sender, PaintEventArgs e)
    {
        using var line = new SolidBrush(DividerColor);
        float top = 0;

        for (var index = 0; index < _rows.Count; index++)
        {
            var height = RowStyles[index].Height;

            if (height > 0 && NextShown(index) is { } next && !_rows[next].Joined)
                e.Graphics.FillRectangle(line, 0, top + height - SectionPanel.GapBelow + SectionPanel.DividerOffset, Width, S(1));

            top += height;
        }
    }
}
