using System.Drawing.Drawing2D;
using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>One bento card's content: a small caption, the figure, a note, and an optional bar (0 to 1). A card with a Picture shows it at its left, the words beside it, with an Extra line under the note. Wide cards take a whole row. A wide card with a bar has its note at the right of the figure, as large; any other card has it under the figure, or at the right of it with DetailBeside.</summary>
internal sealed record BentoCardSpec(
    string Caption,
    string Value,
    string? Detail,
    bool Wide = false,
    double? Bar = null,
    Color? BarColor = null,
    bool DetailBeside = false,
    Image? Picture = null,
    string? Extra = null,
    Color? ExtraColor = null);

/// <summary>
/// Figures two to a row. A wide card takes a row of its own, and a narrow
/// card left without a partner is widened so no gap is left. Showing the same
/// cards again (the usual refresh) only updates their figures, so nothing
/// flickers.
/// </summary>
internal sealed class BentoGrid : TableLayoutPanel
{
    /// <summary>The space between a pair's figures and bars.</summary>
    private static int ColumnGap => S(24);

    /// <summary>The room for a card's picture, at its left: wide enough for a laptop seen from the front.</summary>
    public static Size PictureSize => new(S(156), S(100));

    private string _layoutKey = string.Empty;
    private readonly Font _valueFont;
    private readonly bool _compact;

    /// <param name="semiBoldValues">Sets the figures in semi-bold, as in System information.</param>
    /// <param name="compact">Three to a row, close together and with no lines between, in order; the last card of a short row takes the rest of it.</param>
    public BentoGrid(int width, bool semiBoldValues = false, bool compact = false)
    {
        _valueFont = semiBoldValues ? SemiBoldFont(18) : DesignFont(18);
        _compact = compact;

        if (compact)
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = BackgroundColor;
            ColumnCount = CompactColumns;
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            Width = width;

            // The first columns take the gaps at their right, so the three figures are as wide.
            var each = (width - (CompactColumns - 1) * ColumnGap) / CompactColumns;

            for (var column = 0; column < CompactColumns; column++)
                ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, column < CompactColumns - 1 ? each + ColumnGap : width - (CompactColumns - 1) * (each + ColumnGap)));

            return;
        }

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ColumnCount = 2;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        Width = width;

        // The left column takes the gap, so both figures and bars are as wide and
        // the divider above a pair still runs unbroken.
        var right = (width - ColumnGap) / 2;
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width - right));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, right));
    }

    public void ShowCards(IReadOnlyList<BentoCardSpec> specs)
    {
        if (_compact)
        {
            ShowCompact(specs);
            return;
        }

        var cards = specs.ToList();

        for (var index = 0; index < cards.Count; index++)
        {
            if (cards[index].Wide)
                continue;

            var next = index + 1 < cards.Count ? cards[index + 1] : null;

            if (next is null || next.Wide)
                cards[index] = cards[index] with { Wide = true };
            else
                index++; // The pair is settled.
        }

        // Same cards in the same places: only the figures change.
        var key = string.Join("|", cards.Select(card => $"{card.Caption}:{card.Wide}:{card.Bar is not null}"));

        if (key == _layoutKey)
        {
            var existing = Controls.OfType<BentoCard>().ToList();

            for (var index = 0; index < cards.Count; index++)
                existing[index].Show(cards[index]);

            return;
        }

        _layoutKey = key;
        SuspendLayout();
        Controls.Clear();
        RowStyles.Clear();

        var row = 0;
        var column = 0;

        foreach (var spec in cards)
        {
            if (spec.Wide && column == 1)
            {
                row++;
                column = 0;
            }

            var card = new BentoCard(_valueFont)
            {
                DividerAbove = row > 0,
                GapRight = !spec.Wide && column == 0 ? ColumnGap : 0,
                Dock = DockStyle.Fill,
                Height = BentoCard.HeightFor(spec, compact: false),
                Margin = Padding.Empty
            };

            card.Show(spec);
            Controls.Add(card, column, row);

            if (spec.Wide)
            {
                SetColumnSpan(card, 2);
                row++;
                column = 0;
            }
            else if (column == 1)
            {
                row++;
                column = 0;
            }
            else
            {
                column = 1;
            }
        }

        RowCount = column == 1 ? row + 1 : row;

        for (var index = 0; index < RowCount; index++)
            RowStyles.Add(new RowStyle(SizeType.AutoSize));

        ResumeLayout();
    }

    private const int CompactColumns = 3;

    // Three to a row, in order; the last card of a short row reaches to the end of it.
    private void ShowCompact(IReadOnlyList<BentoCardSpec> cards)
    {
        var key = string.Join("|", cards.Select(card => $"{card.Caption}:{card.Bar is not null}"));

        if (key == _layoutKey)
        {
            var existing = Controls.OfType<BentoCard>().ToList();

            for (var index = 0; index < cards.Count; index++)
                existing[index].Show(cards[index]);

            return;
        }

        _layoutKey = key;
        SuspendLayout();
        Controls.Clear();
        RowStyles.Clear();

        for (var index = 0; index < cards.Count; index++)
        {
            var (row, column) = (index / CompactColumns, index % CompactColumns);
            var span = index == cards.Count - 1 ? CompactColumns - column : 1;

            var card = new BentoCard(_valueFont)
            {
                Compact = true,
                GapRight = column + span < CompactColumns ? ColumnGap : 0,
                Dock = DockStyle.Fill,
                Height = BentoCard.HeightFor(cards[index], compact: true),
                Margin = Padding.Empty
            };

            card.Show(cards[index]);
            Controls.Add(card, column, row);
            SetColumnSpan(card, span);
        }

        RowCount = (cards.Count + CompactColumns - 1) / CompactColumns;

        for (var index = 0; index < RowCount; index++)
            RowStyles.Add(new RowStyle(SizeType.AutoSize));

        ResumeLayout();
    }

    /// <summary>
    /// One figure, flat on the window: a divider line above it (but for the
    /// first), the caption in quiet grey, then the figure in white (a wide
    /// card's note at the right of it, a narrow card's under it), and an
    /// optional bar.
    /// </summary>
    private sealed class BentoCard : Control
    {
        private static readonly Font CaptionFont = DesignFont(14);
        private static readonly Font DetailFont = DesignFont(14);

        // Compact, the figures sit closer: rows apart by their gaps alone, with no line between.
        private static int TopGap(bool compact) => S(compact ? 6 : 18);
        private static int BottomGap(bool compact) => S(compact ? 14 : 18);

        /// <summary>How tall a card is: the gap above, the caption, the figure, the bar or the note, and the gap below.</summary>
        public static int HeightFor(BentoCardSpec spec, bool compact) => spec.Picture is { } picture
            ? TopGap(compact) + Math.Max(picture.Height, WordsHeight(spec)) + BottomGap(compact)
            : TopGap(compact) + S(20 + 4 + 24) + (spec.Bar is null ? 0 : S(8 + 6)) + (NoteUnder(spec) ? S(20) : 0) + BottomGap(compact);

        private static int PictureGap => S(22);

        // The words beside a picture: the caption, the figure, the note and the extra line.
        private static int WordsHeight(BentoCardSpec spec) =>
            S(20 + 4 + 24) + (spec.Detail is null ? 0 : S(20)) + (spec.Extra is null ? 0 : S(4 + 20));

        // A wide card with a bar (a drive) has its note at the right of the figure; every other note goes under it, unless asked beside.
        private static bool NoteRight(BentoCardSpec spec) => spec.Wide && spec.Bar is not null;

        private static bool NoteUnder(BentoCardSpec spec) => spec.Detail is not null && !NoteRight(spec) && !spec.DetailBeside;

        private BentoCardSpec? _spec;

        private readonly Font _valueFont;

        public BentoCard(Font valueFont)
        {
            _valueFont = valueFont;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>Closer to its neighbours, with smaller gaps above and below.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Compact { get; init; }

        /// <summary>Whether a divider line runs along the top: every card but those in the first row.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool DividerAbove { get; init; }

        /// <summary>Room kept clear at the right, for the gap to a right-hand neighbour; the divider still runs through it.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int GapRight { get; init; }

        public void Show(BentoCardSpec spec)
        {
            if (spec == _spec)
                return;

            _spec = spec;
            AccessibleName = $"{L.T(spec.Caption)}: {spec.Value} {spec.Detail}";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.Clear(Parent?.BackColor ?? BackgroundColor);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (DividerAbove)
            {
                using var line = new SolidBrush(DividerColor);
                graphics.FillRectangle(line, 0, 0, Width, S(1));
            }

            if (_spec is not { } spec)
                return;

            const TextFormatFlags Line = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

            var left = 0;
            var y = TopGap(Compact);

            if (spec.Picture is { } picture)
            {
                graphics.DrawImageUnscaled(picture, 0, (Height - picture.Height) / 2);
                left = picture.Width + PictureGap;
                y = (Height - WordsHeight(spec)) / 2;
            }

            var width = Width - GapRight - left;

            TextRenderer.DrawText(graphics, L.T(spec.Caption), CaptionFont, new Rectangle(left, y, width, S(20)), SubtleTextColor, Line);
            y += S(20 + 4);

            var valueRow = new Rectangle(left, y, width, S(24));
            TextRenderer.DrawText(graphics, spec.Value, _valueFont, valueRow, Color.White, Line);
            y += S(24);

            if (spec.Detail is { } detail)
            {
                if (NoteRight(spec))
                {
                    TextRenderer.DrawText(graphics, detail, _valueFont, valueRow, Color.White, Line | TextFormatFlags.Right);
                }
                else if (spec.DetailBeside)
                {
                    // The note at its own size, at the right of the figure's row.
                    TextRenderer.DrawText(graphics, detail, DetailFont, valueRow, SubtleTextColor, Line | TextFormatFlags.Right);
                }
                else
                {
                    TextRenderer.DrawText(graphics, detail, DetailFont, new Rectangle(left, y, width, S(20)), SubtleTextColor, Line);
                    y += S(20);
                }
            }

            if (spec.Extra is { } extra)
            {
                TextRenderer.DrawText(graphics, extra, DetailFont, new Rectangle(left, y + S(4), width, S(20)), spec.ExtraColor ?? SubtleTextColor, Line);
                y += S(4 + 20);
            }

            if (spec.Bar is { } fraction)
            {
                var bar = new RectangleF(left, y + S(8), width, S(6));

                using (var track = new SolidBrush(TrackColor))
                using (var trackShape = RoundedButton.RoundedPath(bar, S(3)))
                    graphics.FillPath(track, trackShape);

                var filled = bar with { Width = (float)(bar.Width * Math.Clamp(fraction, 0, 1)) };

                if (filled.Width >= S(6))
                {
                    // Flat, in the level's color, as on the sliders.
                    using var level = new SolidBrush(spec.BarColor ?? RazerGreen);
                    using var levelShape = RoundedButton.RoundedPath(filled, S(3));
                    graphics.FillPath(level, levelShape);
                }
            }
        }
    }
}
