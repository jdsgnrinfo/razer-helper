using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI;

/// <summary>
/// The main window's frosted background: the window's own color at 90 %
/// over Windows' blur of whatever is behind it (the acrylic of the Start
/// menu). Windows does the blur on the graphics card, so the app does no
/// extra work for it.
///
/// How Windows composes it: a pixel painted pure black is see-through, so
/// the window's background is painted black and the tinted blur shows there.
/// Fills drawn with GDI+ stay solid. Text drawn with GDI (TextRenderer, and a
/// plain Label) comes out lightened by what is behind it, so on glass text is
/// drawn with GDI+ instead (<see cref="DrawText"/>). When Windows'
/// transparency effects are off, the window shows the plain tint: the same
/// color as without glass.
/// </summary>
internal static class Glass
{
    /// <summary>The see-through color: whatever is painted this shows the frosted background.</summary>
    public static readonly Color SeeThrough = Color.Black;

    // 90 % of the window's color over the blur.
    private const byte TintAlpha = 0xE6;

    private static readonly ConditionalWeakTable<Form, object> Windows = new();

    /// <summary>
    /// Gives <paramref name="form"/> the frosted background: everything in it
    /// that has the window's color becomes see-through, and its labels draw
    /// their text with GDI+. Controls added later are converted as they come.
    /// </summary>
    public static void Apply(Form form)
    {
        Windows.AddOrUpdate(form, new object());
        Convert(form);

        if (form.IsHandleCreated)
            ApplyAccent(form.Handle);
        else
            form.HandleCreated += (_, _) => ApplyAccent(form.Handle);
    }

    /// <summary>Whether <paramref name="control"/> sits on a frosted window.</summary>
    public static bool IsOn(Control control) => control.FindForm() is { } form && Windows.TryGetValue(form, out _);

    /// <summary>
    /// Draws text as TextRenderer.DrawText does, but with GDI+ on a frosted
    /// window, where GDI's text would come out lightened by what is behind it.
    /// </summary>
    public static void DrawText(Graphics graphics, Control owner, string? text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
    {
        if (!IsOn(owner))
        {
            TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
            return;
        }

        if (string.IsNullOrEmpty(text))
            return;

        using var format = new StringFormat(StringFormat.GenericDefault)
        {
            Alignment = flags.HasFlag(TextFormatFlags.HorizontalCenter) ? StringAlignment.Center
                : flags.HasFlag(TextFormatFlags.Right) ? StringAlignment.Far
                : StringAlignment.Near,
            LineAlignment = flags.HasFlag(TextFormatFlags.VerticalCenter) ? StringAlignment.Center
                : flags.HasFlag(TextFormatFlags.Bottom) ? StringAlignment.Far
                : StringAlignment.Near,
            Trimming = flags.HasFlag(TextFormatFlags.EndEllipsis) ? StringTrimming.EllipsisCharacter : StringTrimming.None
        };

        if (!flags.HasFlag(TextFormatFlags.WordBreak))
            format.FormatFlags |= StringFormatFlags.NoWrap;

        var hint = graphics.TextRenderingHint;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using (var brush = new SolidBrush(color))
            graphics.DrawString(text, font, brush, bounds, format);

        graphics.TextRenderingHint = hint;
    }

    /// <summary>
    /// Graphics.Clear, except that the see-through color is cleared with GDI:
    /// GDI+ would paint it as solid black, GDI leaves it see-through.
    /// </summary>
    public static void Clear(Graphics graphics, Color color)
    {
        if (color.ToArgb() != SeeThrough.ToArgb())
        {
            graphics.Clear(color);
            return;
        }

        var hdc = graphics.GetHdc();

        try
        {
            var everything = new Rect { Right = short.MaxValue, Bottom = short.MaxValue };
            FillRect(hdc, ref everything, GetStockObject(BlackBrush));
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }

    private static void Convert(Control control)
    {
        if (control.BackColor.ToArgb() == BackgroundColor.ToArgb())
            control.BackColor = SeeThrough;

        // A plain label's text is GDI's; this switch makes it GDI+'s.
        if (control is Label label)
            label.UseCompatibleTextRendering = true;

        foreach (Control child in control.Controls)
            Convert(child);

        control.ControlAdded += (_, e) =>
        {
            if (e.Control is { } added)
                Convert(added);
        };
    }

    // Windows' acrylic, tinted with the window's color.
    private static void ApplyAccent(IntPtr handle)
    {
        var accent = new AccentPolicy
        {
            State = AccentEnableAcrylicBlurBehind,
            Flags = AccentFlagDrawAllBorders,
            // ABGR.
            GradientColor = (uint)(TintAlpha << 24 | BackgroundColor.B << 16 | BackgroundColor.G << 8 | BackgroundColor.R)
        };

        var size = Marshal.SizeOf<AccentPolicy>();
        var pointer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(accent, pointer, false);
            var data = new CompositionAttributeData { Attribute = WcaAccentPolicy, Data = pointer, SizeOfData = size };
            SetWindowCompositionAttribute(handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private const int WcaAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;
    private const int AccentFlagDrawAllBorders = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int State;
        public int Flags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int BlackBrush = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hdc, ref Rect rect, IntPtr brush);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr window, ref CompositionAttributeData data);
}
