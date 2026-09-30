using System.Drawing.Drawing2D;
using RazerHelper.Core.Localization;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>One bento card's content: a small caption, the figure, a note under it, and an optional bar (0 to 1). Wide cards take a whole row.</summary>
internal sealed record BentoCardSpec(
    string Caption,
    string Value,
    string? Detail,
    bool Wide = false,
    double? Bar = null,
    Color? BarColor = null);

/// <summary>
/// Figures two to a row. A wide card takes a row of its own, and a narrow
/// card left without a partner is widened so no gap is left. Showing the same
/// cards again (the usual refresh) only updates their figures, so nothing
/// flickers.
/// </summary>
internal sealed class BentoGrid : TableLayoutPanel
{
    private string _layoutKey = string.Empty;

    public BentoGrid(int width)
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = BackgroundColor;
        ColumnCount = 2;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        Width = width;

        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width / 2));
        ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width - width / 2));
    }

    public void ShowCards(IReadOnlyList<BentoCardSpec> specs)
    {
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

            var card = new BentoCard
            {
                DividerAbove = row > 0,
                Dock = DockStyle.Fill,
                Height = BentoCard.HeightFor(spec),
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

    /// <summary>
    /// One figure, flat on the window: a divider line above it (but for the
    /// first), the caption in quiet grey, then the figure in white (a wide
    /// card's note at the right of it, a narrow card's under it), and an
    /// optional bar.
    /// </summary>
    private sealed class BentoCard : Control
    {
        private static readonly Font CaptionFont = DesignFont(14);
        private static readonly Font ValueFont = DesignFont(18);
        private static readonly Font DetailFont = DesignFont(14);

        private static int TopGap => S(18);
        private static int BottomGap => S(18);

        /// <summary>How tall a card is: the gap above, the caption, the figure, the bar or the note, and the gap below.</summary>
        public static int HeightFor(BentoCardSpec spec) =>
            TopGap + S(20 + 4 + 24) + (spec.Bar is null ? 0 : S(8 + 6)) + (!spec.Wide && spec.Detail is not null ? S(20) : 0) + BottomGap;

        private BentoCardSpec? _spec;

        public BentoCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        /// <summary>Whether a divider line runs along the top: every card but those in the first row.</summary>
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool DividerAbove { get; init; }

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

            var width = Width - S(8); // Keeps a narrow card's text off its neighbour.
            var y = TopGap;

            TextRenderer.DrawText(graphics, L.T(spec.Caption), CaptionFont, new Rectangle(0, y, width, S(20)), SubtleTextColor, Line);
            y += S(20 + 4);

            var valueRow = new Rectangle(0, y, Width, S(24));
            TextRenderer.DrawText(graphics, spec.Value, ValueFont, valueRow, Color.White, Line);
            y += S(24);

            if (spec.Detail is { } detail)
            {
                if (spec.Wide)
                {
                    TextRenderer.DrawText(graphics, detail, ValueFont, valueRow, Color.White, Line | TextFormatFlags.Right);
                }
                else
                {
                    TextRenderer.DrawText(graphics, detail, DetailFont, new Rectangle(0, y, width, S(20)), SubtleTextColor, Line);
                    y += S(20);
                }
            }

            if (spec.Bar is { } fraction)
            {
                var bar = new RectangleF(0, y + S(8), Width, S(6));

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
