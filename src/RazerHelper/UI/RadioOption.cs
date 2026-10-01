using System.Drawing.Drawing2D;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// One choice of a pair, such as the fans' Automatic and Max RPM: a round
/// marker, then a title with a quiet description under it. The marker is a
/// green ring around a dark dot when chosen and a plain grey dot otherwise.
/// It is a Button underneath, so choosing, greying and the tooltip work with
/// <see cref="UiControls.HighlightSelected"/> and <see cref="UiControls.SetAvailability"/>
/// exactly as on the other buttons: a green BackColor marks the chosen one.
/// </summary>
internal sealed class RadioOption : Button
{
    private static int MarkerSize => S(20);
    private static int TextLeft => S(32);

    private static readonly Font TitleFont = SemiBoldFont(16);
    private static readonly Font DescriptionFont = DesignFont(13);

    private bool _hovered;

    public RadioOption(string title, string description)
    {
        Text = title;
        Description = description;

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
    }

    public string Description { get; }

    /// <summary>The height a title and its description need.</summary>
    public static int PreferredHeight => S(44);

    private bool IsChosen => BackColor.ToArgb() == RazerGreen.ToArgb();

    // Greyed by SetAvailability, which also takes away the hand cursor.
    private bool IsUsable => Enabled && Cursor == Cursors.Hand;

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        graphics.Clear(Parent?.BackColor ?? BackgroundColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var titleHeight = S(24);
        var marker = new RectangleF(0.5f, (titleHeight - MarkerSize) / 2f, MarkerSize - 1, MarkerSize - 1);

        if (IsChosen)
        {
            using var ring = new SolidBrush(IsUsable ? RazerGreen : OffColor);
            using var dot = new SolidBrush(Color.Black);
            var inset = S(5.5f);
            graphics.FillEllipse(ring, marker);
            graphics.FillEllipse(dot, RectangleF.Inflate(marker, -inset, -inset));
        }
        else
        {
            using var plain = new SolidBrush(_hovered && IsUsable ? Color.FromArgb(0x6A, 0x6A, 0x6A) : Color.FromArgb(0x4D, 0x4D, 0x4D));
            graphics.FillEllipse(plain, marker);
        }

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Color.Silver);
            graphics.DrawEllipse(focus, RectangleF.Inflate(marker, 2, 2));
        }

        var usable = IsUsable || IsChosen;
        const TextFormatFlags Line = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

        TextRenderer.DrawText(graphics, Text, TitleFont, new Rectangle(TextLeft, 0, Width - TextLeft, titleHeight),
            usable ? Color.White : SystemColors.GrayText, Line);
        TextRenderer.DrawText(graphics, Description, DescriptionFont, new Rectangle(TextLeft, titleHeight, Width - TextLeft, Height - titleHeight),
            usable ? SubtleTextColor : SystemColors.GrayText, Line);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }
}
