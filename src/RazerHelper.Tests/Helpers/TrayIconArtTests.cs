using RazerHelper.Core.Models;
using RazerHelper.UI;

namespace RazerHelper.Tests.Helpers;

public class TrayIconArtTests
{
    [Fact]
    public void TintBitmap_RepaintsTheGreenAndLeavesTheBlackMark()
    {
        using var source = new Bitmap(2, 1);
        source.SetPixel(0, 0, Color.FromArgb(255, 0, 255, 0));
        source.SetPixel(1, 0, Color.FromArgb(255, 0, 0, 0));

        using var tinted = TrayIconArt.TintBitmap(source, Color.FromArgb(0x2F, 0x8C, 0xFF));

        Assert.Equal(Color.FromArgb(255, 0x2F, 0x8C, 0xFF), tinted.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(255, 0, 0, 0), tinted.GetPixel(1, 0));
    }

    [Fact]
    public void TintBitmap_KeepsASoftEdgeSoftAndTransparencyAsItIs()
    {
        using var source = new Bitmap(2, 1);
        source.SetPixel(0, 0, Color.FromArgb(255, 0, 128, 0)); // Half green, half black.
        source.SetPixel(1, 0, Color.FromArgb(0, 0, 0, 0));

        using var tinted = TrayIconArt.TintBitmap(source, Color.FromArgb(200, 100, 0));

        var edge = tinted.GetPixel(0, 0);
        Assert.InRange(edge.R, 99, 101);
        Assert.InRange(edge.G, 49, 51);
        Assert.Equal(0, tinted.GetPixel(1, 0).A);
    }

    // In English, which the tests run in: switching the language here would
    // leak into the tests running beside this one.
    [Fact]
    public void HoverText_NamesTheModeInBrackets()
    {
        Assert.Equal("RazerHelper (Gaming)", TrayIconHost.HoverText(PerformanceMode.Gaming));
        Assert.Equal("RazerHelper", TrayIconHost.HoverText(null));
    }
}
