using System.Globalization;
using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class SystemInfoTextTests
{
    [Theory]
    [InlineData("Intel(R) Core(TM) i7-10750H CPU @ 2.60GHz", "Intel Core i7-10750H")]
    [InlineData("AMD Ryzen 7 4800H with Radeon Graphics", "AMD Ryzen 7 4800H")]
    [InlineData("12th Gen Intel(R) Core(TM) i7-12800H", "12th Gen Intel Core i7-12800H")]
    public void CpuName_DropsTheMarksAndTheBaseClock(string name, string expected) =>
        Assert.Equal(expected, SystemInfoText.CpuName(name));

    [Theory]
    [InlineData("NVIDIA GeForce GTX 1660 Ti", "GeForce GTX 1660 Ti")]
    [InlineData("Intel(R) UHD Graphics", "UHD Graphics")]
    [InlineData("AMD Radeon RX 6800M", "Radeon RX 6800M")]
    [InlineData("Microsoft Basic Display Adapter", "Microsoft Basic Display Adapter")]
    public void GpuName_DropsTheMaker(string name, string expected) =>
        Assert.Equal(expected, SystemInfoText.GpuName(name));

    [Fact]
    public void OsName_CallsWindows11ByItsName() =>
        Assert.Equal("Windows 11 Home", SystemInfoText.OsName("Windows 10 Home", 26200));

    [Fact]
    public void OsName_LeavesWindows10Alone() =>
        Assert.Equal("Windows 10 Pro", SystemInfoText.OsName("Windows 10 Pro", 19045));

    [Fact]
    public void OsDetail_GivesTheVersionAndBuild() =>
        Assert.Equal("25H2 (build 26200.9457)", SystemInfoText.OsDetail("25H2", 26200, 9457));

    [Fact]
    public void CpuDetail_GivesCoresAndThreads() =>
        Assert.Equal("6 cores · 12 threads", SystemInfoText.CpuDetail(6, 12));

    [Theory]
    [InlineData(500107862016L, "466 GB")]
    [InlineData(1000204886016L, "932 GB")]
    [InlineData(25769803776L, "24 GB")]
    [InlineData(4294967296L, "4.0 GB")]
    [InlineData(2199023255552L, "2.0 TB")]
    public void Size_IsInBinaryUnitsLikeWindows(long bytes, string expected) =>
        Assert.Equal(expected, SystemInfoText.Size(bytes, CultureInfo.InvariantCulture));

    [Fact]
    public void BiosDate_IsInTheReadersFormat() =>
        Assert.Equal("4/6/2020", SystemInfoText.BiosDate("06/04/2020", CultureInfo.GetCultureInfo("es-ES")));
}
