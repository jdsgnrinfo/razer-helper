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
/// Bento cards two to a row. A wide card takes a row of its own, and a narrow
/// card left without a partner is widened so no gap is left. Showing the same
/// cards again (the usual refresh) only updates their figures, so nothing
/// flickers.
/// </summary>
internal sealed class BentoGrid : TableLayoutPanel
{
    private static int CardGap => S(8);

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
                Dock = DockStyle.Fill,
                Height = spec.Bar is null ? S(76) : S(90),
                Margin = new Padding(
                    column == 1 ? CardGap / 2 : 0,
                    row == 0 ? 0 : CardGap / 2,
                    spec.Wide || column == 1 ? 0 : CardGap / 2,
                    CardGap / 2)
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

    /// <summary>One card: the caption, the figure in bold, a note under it, and an optional bar.</summary>
    private sealed class BentoCard : Control
    {
        private static readonly Font CaptionFont = GetDesignFont("Segoe UI", 8.5F);
        private static readonly Font ValueFont = GetDesignFont("Segoe UI", 13F, FontStyle.Bold);
        private static readonly Font DetailFont = GetDesignFont("Segoe UI", 8.5F);
        private static readonly Color TrackColor = Color.FromArgb(34, 34, 34);

        private BentoCardSpec? _spec;

        public BentoCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

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

            // The same corner radius as the buttons, so every rounded shape matches.
            using (var fill = new SolidBrush(CardColor))
            using (var shape = RoundedButton.RoundedPath(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), S(RoundedButton.CornerRadius)))
                graphics.FillPath(fill, shape);

            if (_spec is not { } spec)
                return;

            var inset = S(12);
            var width = Width - inset * 2;
            var y = S(10);

            TextRenderer.DrawText(graphics, L.T(spec.Caption).ToUpper(System.Globalization.CultureInfo.CurrentCulture), CaptionFont, new Rectangle(inset, y, width, S(16)), SubtleTextColor,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            y += S(17);

            TextRenderer.DrawText(graphics, spec.Value, ValueFont, new Rectangle(inset, y, width, S(26)), Color.White,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            y += S(25);

            if (spec.Detail is { } detail)
            {
                TextRenderer.DrawText(graphics, detail, DetailFont, new Rectangle(inset, y, width, S(16)), SubtleTextColor,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }

            if (spec.Bar is { } fraction)
            {
                var bar = new RectangleF(inset, Height - S(16), width, S(6));

                using (var track = new SolidBrush(TrackColor))
                using (var trackShape = RoundedButton.RoundedPath(bar, S(3)))
                    graphics.FillPath(track, trackShape);

                var filled = bar with { Width = (float)(bar.Width * Math.Clamp(fraction, 0, 1)) };

                if (filled.Width >= S(6))
                {
                    using var level = new SolidBrush(spec.BarColor ?? RazerGreen);
                    using var levelShape = RoundedButton.RoundedPath(filled, S(3));
                    graphics.FillPath(level, levelShape);
                }
            }
        }
    }
}
