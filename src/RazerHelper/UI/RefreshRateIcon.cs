using System.Drawing.Drawing2D;

namespace RazerHelper.UI;

/// <summary>
/// The refresh-rate buttons' icon: the rate in square, blocky digits ("60",
/// "120"; "A" for Auto) over a row of three thin bars and a wide block, a
/// sign of motion. Built from rectangles, so any rate the panel offers gets
/// one and it stays sharp at every scale.
/// </summary>
internal static class RefreshRateIcon
{
    // Design units: a digit is 10 by 14 with strokes 3.4 thick.
    private const float DigitWidth = 10f;
    private const float DigitHeight = 14f;
    private const float Stroke = 3.4f;
    private const float DigitGap = 3f;
    private const float BarsGap = 5f;
    private const float BarsHeight = 6f;
    private const float Corner = 0.8f;

    // The thin bars and their gaps, as shares of the icon's width; the block takes the rest.
    private const int ThinBars = 3;
    private const float ThinBarShare = 0.08f;
    private const float BarGapShare = 0.075f;

    private const float TotalHeight = DigitHeight + BarsGap + BarsHeight;

    /// <summary>The icon for a rate, or for Auto when <paramref name="hertz"/> is null.</summary>
    public static Action<Graphics, RectangleF, Color> For(int? hertz)
    {
        var text = hertz?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "A";
        return (graphics, bounds, color) => Draw(graphics, text, bounds, color);
    }

    /// <summary>
    /// Draws <paramref name="text"/> (digits, or A) and the bars. The height
    /// fills <paramref name="bounds"/>, so every rate has digits the same size;
    /// a long rate is narrowed to stay within 1.5 times the box's width.
    /// </summary>
    public static void Draw(Graphics graphics, string text, RectangleF bounds, Color color)
    {
        // The bars are never narrower than two digits, so Auto's lone A sits
        // centred over a row as wide as 60's.
        var textWidth = MeasureText(text);
        var width = Math.Max(textWidth, DigitWidth * 2 + DigitGap);

        using var path = new GraphicsPath(FillMode.Winding);
        var x = (width - textWidth) / 2f;

        foreach (var character in text)
            x += AddCharacter(path, character, x) + DigitGap;

        AddBars(path, width);

        var scale = Math.Min(bounds.Height / TotalHeight, bounds.Width * 1.5f / width);
        var state = graphics.Save();

        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(
                bounds.X + (bounds.Width - width * scale) / 2f,
                bounds.Y + (bounds.Height - TotalHeight * scale) / 2f);
            graphics.ScaleTransform(scale, scale);

            using var brush = new SolidBrush(color);
            graphics.FillPath(brush, path);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    // Seven blocky strokes, as on a digital clock but solid and square.
    [Flags]
    private enum Strokes
    {
        Top = 1,
        Middle = 2,
        Bottom = 4,
        UpperLeft = 8,
        UpperRight = 16,
        LowerLeft = 32,
        LowerRight = 64
    }

    private static Strokes StrokesOf(char character) => character switch
    {
        '0' => Strokes.Top | Strokes.Bottom | Strokes.UpperLeft | Strokes.UpperRight | Strokes.LowerLeft | Strokes.LowerRight,
        '2' => Strokes.Top | Strokes.UpperRight | Strokes.Middle | Strokes.LowerLeft | Strokes.Bottom,
        '3' => Strokes.Top | Strokes.UpperRight | Strokes.Middle | Strokes.LowerRight | Strokes.Bottom,
        '4' => Strokes.UpperLeft | Strokes.UpperRight | Strokes.Middle | Strokes.LowerRight,
        '5' => Strokes.Top | Strokes.UpperLeft | Strokes.Middle | Strokes.LowerRight | Strokes.Bottom,
        '6' => Strokes.Top | Strokes.UpperLeft | Strokes.Middle | Strokes.LowerLeft | Strokes.LowerRight | Strokes.Bottom,
        '7' => Strokes.Top | Strokes.UpperRight | Strokes.LowerRight,
        '8' => Strokes.Top | Strokes.Middle | Strokes.Bottom | Strokes.UpperLeft | Strokes.UpperRight | Strokes.LowerLeft | Strokes.LowerRight,
        '9' => Strokes.Top | Strokes.UpperLeft | Strokes.UpperRight | Strokes.Middle | Strokes.LowerRight | Strokes.Bottom,
        'A' => Strokes.Top | Strokes.UpperLeft | Strokes.UpperRight | Strokes.Middle | Strokes.LowerLeft | Strokes.LowerRight,
        _ => 0
    };

    // A one is a stem with a short flag to its left, narrower than the rest.
    private const float OneFlag = 2.6f;

    private static float WidthOf(char character) => character == '1' ? OneFlag + Stroke : DigitWidth;

    private static float MeasureText(string text) =>
        text.Sum(WidthOf) + DigitGap * Math.Max(0, text.Length - 1);

    // Adds one character at x and returns its width.
    private static float AddCharacter(GraphicsPath path, char character, float x)
    {
        if (character == '1')
        {
            AddBlock(path, x, 0, OneFlag + Stroke, Stroke);
            AddBlock(path, x + OneFlag, 0, Stroke, DigitHeight);
            return WidthOf(character);
        }

        var strokes = StrokesOf(character);
        var middle = (DigitHeight - Stroke) / 2f;
        var right = x + DigitWidth - Stroke;

        if (strokes.HasFlag(Strokes.Top))
            AddBlock(path, x, 0, DigitWidth, Stroke);
        if (strokes.HasFlag(Strokes.Middle))
            AddBlock(path, x, middle, DigitWidth, Stroke);
        if (strokes.HasFlag(Strokes.Bottom))
            AddBlock(path, x, DigitHeight - Stroke, DigitWidth, Stroke);
        if (strokes.HasFlag(Strokes.UpperLeft))
            AddBlock(path, x, 0, Stroke, middle + Stroke);
        if (strokes.HasFlag(Strokes.UpperRight))
            AddBlock(path, right, 0, Stroke, middle + Stroke);
        if (strokes.HasFlag(Strokes.LowerLeft))
            AddBlock(path, x, middle, Stroke, DigitHeight - middle);
        if (strokes.HasFlag(Strokes.LowerRight))
            AddBlock(path, right, middle, Stroke, DigitHeight - middle);

        return DigitWidth;
    }

    private static void AddBars(GraphicsPath path, float width)
    {
        var top = DigitHeight + BarsGap;
        var thin = width * ThinBarShare;
        var gap = width * BarGapShare;
        var x = 0f;

        for (var bar = 0; bar < ThinBars; bar++)
        {
            AddBlock(path, x, top, thin, BarsHeight);
            x += thin + gap;
        }

        AddBlock(path, x, top, width - x, BarsHeight);
    }

    // Slightly rounded, so the outer corners are soft; where strokes meet they overlap and fill as one.
    private static void AddBlock(GraphicsPath path, float x, float y, float width, float height)
    {
        using var block = RoundedButton.RoundedPath(new RectangleF(x, y, width, height), Corner);
        path.AddPath(block, connect: false);
    }
}
