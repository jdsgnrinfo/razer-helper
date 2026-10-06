using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The laptop's picture for the System page: its photo from Assets\Laptops
/// (inside the exe), the closest there to the model (see
/// <see cref="LaptopPictureNames"/>), or, with none, a laptop drawn in fine
/// lines. Each is made once at the size asked, in the window's pixels, with
/// the finest resampling, so it is never stretched while painting.
/// </summary>
internal static class LaptopPictures
{
    private const string ResourcePrefix = "Laptops.";

    // The drawing's own grid, 300 by 190.
    private static readonly SizeF Art = new(300, 190);

    private static readonly Dictionary<(string Model, Size Size), Bitmap> Made = [];

    /// <summary>The picture for <paramref name="model"/>, fitted inside <paramref name="size"/> and centred.</summary>
    public static Bitmap For(string? model, Size size)
    {
        var key = (model ?? string.Empty, size);

        if (Made.TryGetValue(key, out var made))
            return made;

        var picture = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);

        using (var graphics = Graphics.FromImage(picture))
        {
            graphics.Clear(Color.Transparent);

            if (Load(model) is { } photo)
            {
                using (photo)
                    Fit(graphics, photo, size);
            }
            else
            {
                // The drawing at the photos' proportions, a little in from the edges.
                var width = Math.Min(size.Width, size.Height * Art.Width / Art.Height) * 0.86f;
                var height = width * Art.Height / Art.Width;
                PaintDrawing(graphics, new RectangleF((size.Width - width) / 2, (size.Height - height) / 2, width, height));
            }
        }

        Made[key] = picture;
        return picture;
    }

    // The first photo there of the names to try, or null.
    private static Bitmap? Load(string? model)
    {
        var assembly = typeof(LaptopPictures).Assembly;

        foreach (var name in LaptopPictureNames.CandidatesFor(model))
        {
            using var stream = assembly.GetManifestResourceStream($"{ResourcePrefix}{name}.png");

            if (stream is not null)
                return new Bitmap(stream);
        }

        return null;
    }

    // The photo shrunk to fit, in steps of at most half so no detail is skipped, then once more to the exact size.
    private static void Fit(Graphics graphics, Bitmap photo, Size size)
    {
        var scale = Math.Min(size.Width / (float)photo.Width, size.Height / (float)photo.Height);
        var target = new Size(Math.Max(1, (int)Math.Round(photo.Width * scale)), Math.Max(1, (int)Math.Round(photo.Height * scale)));

        Bitmap current = photo;

        while (current.Width / 2 > target.Width && current.Height / 2 > target.Height)
        {
            var half = Resample(current, new Size(current.Width / 2, current.Height / 2));

            if (!ReferenceEquals(current, photo))
                current.Dispose();

            current = half;
        }

        using (var final = Resample(current, target))
            graphics.DrawImageUnscaled(final, (size.Width - target.Width) / 2, (size.Height - target.Height) / 2);

        if (!ReferenceEquals(current, photo))
            current.Dispose();
    }

    // A copy at another size, premultiplied so the cut-out's edges take no dark or light fringe.
    private static Bitmap Resample(Image source, Size size)
    {
        var copy = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);

        using var graphics = Graphics.FromImage(copy);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        // Edge pixels are repeated outwards rather than blended with nothing, so the border stays sharp.
        using var attributes = new ImageAttributes();
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(source, new Rectangle(Point.Empty, size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);

        return copy;
    }

    // The laptop open, seen a little from above, in fine light lines: the
    // lid with its screen in green, the deck with rows of keys, the
    // touchpad and the green light at the front of the keyboard.
    private static void PaintDrawing(Graphics graphics, RectangleF bounds)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.X, bounds.Y);
        graphics.ScaleTransform(bounds.Width / Art.Width, bounds.Height / Art.Height);

        // The lines keep their width in the window's pixels, whatever the drawing's size.
        var unit = Art.Width / bounds.Width;

        using (var lid = SvgPath.Parse("M62 14h176a6 6 0 0 1 6 6v112H56V20a6 6 0 0 1 6-6z"))
        using (var pen = new Pen(Color.FromArgb(0xD8, 0xD8, 0xD8), S(1.3f) * unit) { LineJoin = LineJoin.Round })
            graphics.DrawPath(pen, lid);

        using (var screen = SvgPath.Parse("M70 26h160v94H70z"))
        using (var glow = new SolidBrush(Color.FromArgb(20, RazerGreen)))
        using (var pen = new Pen(Color.FromArgb(178, RazerGreen), S(1.3f) * unit))
        {
            graphics.FillPath(glow, screen);
            graphics.DrawPath(pen, screen);
        }

        using (var deck = SvgPath.Parse("M56 132h188l38 40H18z"))
        using (var pen = new Pen(Color.FromArgb(0xD8, 0xD8, 0xD8), S(1.3f) * unit) { LineJoin = LineJoin.Round })
            graphics.DrawPath(pen, deck);

        using (var keys = SvgPath.Parse("M66 138h168l6 7H60zM60 145h180l6 7H54zM54 152h192l5 6H49zM92 138l-4 20M118 138l-3 20M144 138l-1 20M170 138v20M196 138l2 20M222 138l3 20"))
        using (var pen = new Pen(Color.FromArgb(0x4A, 0x4A, 0x4A), S(1f) * unit))
            graphics.DrawPath(pen, keys);

        using (var pad = SvgPath.Parse("M126 160h48l3 7h-54z"))
        using (var pen = new Pen(Color.FromArgb(0x66, 0x66, 0x66), S(1f) * unit))
            graphics.DrawPath(pen, pad);

        using (var light = new Pen(RazerGreen, S(1.8f) * unit))
            graphics.DrawLine(light, 138, 140, 162, 140);

        using (var mark = SvgPath.Parse("M138 66l12-14 12 14-12 14z"))
        using (var pen = new Pen(Color.FromArgb(204, RazerGreen), S(1.3f) * unit))
            graphics.DrawPath(pen, mark);

        graphics.Restore(state);
    }
}
