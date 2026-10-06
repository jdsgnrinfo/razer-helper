using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace RazerHelper.UI;

/// <summary>
/// A number whose digits roll when it changes: each place that changed slides
/// its old digit out and the new one in, up when the value grew and down when
/// it fell, while the places that stayed the same hold still. Places are
/// matched from the right, so units stay over units ("95" to "100" rolls all
/// three, opening a new place on the left).
/// </summary>
internal sealed class RollingText
{
    private const double RollMilliseconds = 340;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private string _old = string.Empty;
    private double _started = double.NegativeInfinity;
    private int _direction = 1;

    public string Text { get; private set; } = string.Empty;

    /// <summary>True while the digits are still turning.</summary>
    public bool Rolling => Progress < 1;

    private double Progress => Math.Min(1, (Clock.Elapsed.TotalMilliseconds - _started) / RollMilliseconds);

    /// <summary>Shows <paramref name="text"/>, rolling to it unless <paramref name="animate"/> is false or nothing changed.</summary>
    public void Set(string text, bool animate = true)
    {
        if (text == Text)
            return;

        if (animate && Text.Length > 0 && !Motion.Reduced)
        {
            _direction = Number(text) is { } now && Number(Text) is { } before && now < before ? -1 : 1;
            _old = Rolling ? _old : Text;
            _started = Clock.Elapsed.TotalMilliseconds;
        }
        else
        {
            _started = double.NegativeInfinity;
        }

        Text = text;
    }

    /// <summary>
    /// Draws the text in <paramref name="bounds"/>, against the right edge or
    /// the left, or centred, and vertically centred. <paramref name="draw"/>
    /// draws one character at a point with an opacity from 0 to 1; the clip
    /// keeps a rolling digit inside the bounds.
    /// </summary>
    public void Paint(Graphics graphics, RectangleF bounds, HorizontalAlignment alignment, Func<string, SizeF> measure, Action<string, PointF, float> draw)
    {
        var progress = Progress;
        var height = measure("0").Height;
        var top = bounds.Top + (bounds.Height - height) / 2;
        var now = Text;
        var before = progress < 1 ? _old : now;

        // Characters from the right: index 0 is the last one.
        var places = Math.Max(now.Length, before.Length);
        var widths = new float[places];
        var width = 0f;

        for (var place = 0; place < places; place++)
        {
            var newWidth = place < now.Length ? measure(now[now.Length - 1 - place].ToString()).Width : 0;
            var oldWidth = place < before.Length ? measure(before[before.Length - 1 - place].ToString()).Width : 0;
            // A place opening or closing grows or shrinks as the digits turn.
            widths[place] = (float)(oldWidth + (newWidth - oldWidth) * Ease(progress));
            width += widths[place];
        }

        var right = alignment switch
        {
            HorizontalAlignment.Left => bounds.Left + width,
            HorizontalAlignment.Center => bounds.Left + (bounds.Width + width) / 2,
            _ => bounds.Right
        };

        var state = graphics.Save();
        // The digits turn in a window as tall as the text, so one leaving is gone once it is past it.
        graphics.SetClip(new RectangleF(bounds.Left, top - height * 0.08f, bounds.Width, height * 1.16f), CombineMode.Intersect);

        var x = right;
        var travel = height * 0.6f;
        var turn = (float)Ease(progress);

        for (var place = 0; place < places; place++)
        {
            x -= widths[place];
            var newChar = place < now.Length ? now[now.Length - 1 - place].ToString() : null;
            var oldChar = place < before.Length ? before[before.Length - 1 - place].ToString() : null;

            if (newChar == oldChar || progress >= 1)
            {
                if (newChar is not null)
                    draw(newChar, new PointF(x, top), 1);

                continue;
            }

            // The old digit leaves the way the value went; the new one comes in from the other side.
            if (oldChar is not null)
                draw(oldChar, new PointF(x, top - _direction * travel * turn), 1 - turn);

            if (newChar is not null)
                draw(newChar, new PointF(x, top + _direction * travel * (1 - turn)), turn);
        }

        graphics.Restore(state);
    }

    // Eases out with a slight overshoot, like the sliders' spring.
    private static double Ease(double t)
    {
        const double Overshoot = 1.4;
        t = Math.Clamp(t, 0, 1) - 1;
        return 1 + t * t * ((Overshoot + 1) * t + Overshoot);
    }

    // The number a text starts with ("-3.5 dB", "80 %"), to tell which way it moved.
    private static double? Number(string text)
    {
        var digits = new string(text.TakeWhile(c => char.IsDigit(c) || c is '-' or '+' or '.' or ',' or '−').ToArray())
            .Replace('−', '-').Replace(',', '.');

        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
    }
}
