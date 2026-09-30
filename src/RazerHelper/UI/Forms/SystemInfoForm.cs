using System.Globalization;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Forms;

/// <summary>
/// The System information window, opened from the footer: the laptop model
/// as its header, then two to a row Windows and the processor, the graphics,
/// memory (with a bar of how full it is) and the BIOS, and each drive letter
/// with its drive and a bar. The fixed figures
/// are read once in the background; memory and drive space follow along
/// while the window is open.
/// </summary>
internal sealed class SystemInfoForm : DetailsWindow
{
    private readonly Task<SystemInfo> _info;
    private bool _loggedFailure;

    public SystemInfoForm(Func<SystemInfo> read)
        : base("System information")
    {
        _info = Task.Run(read);
    }

    protected override (string Header, IReadOnlyList<BentoCardSpec> Cards) ReadCards()
    {
        var header = L.T("System information");

        if (!_info.IsCompleted)
            return (header, [new BentoCardSpec("System information", L.T("Reading..."), null, Wide: true)]);

        if (!_info.IsCompletedSuccessfully)
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                AppLog.Error("Could not read the system information.", _info.Exception);
            }

            return (header, [new BentoCardSpec("System information", L.T("No information"), L.T("Windows did not report these figures"), Wide: true)]);
        }

        // The model heads the window, where it has the whole width to itself.
        return (_info.Result.Model ?? header, CardsFor(_info.Result, CultureInfo.CurrentCulture));
    }

    private static List<BentoCardSpec> CardsFor(SystemInfo info, CultureInfo culture)
    {
        var cards = new List<BentoCardSpec>();

        if (info.OsName is { } os)
            cards.Add(new BentoCardSpec("Operating system", SystemInfoText.OsName(os, info.OsBuild), SystemInfoText.OsDetail(info.OsVersion, info.OsBuild, info.OsRevision)));

        if (info.CpuName is { } cpu)
            cards.Add(new BentoCardSpec("CPU", SystemInfoText.CpuName(cpu), SystemInfoText.CpuDetail(info.CpuCores, info.CpuThreads)));

        // The built-in graphics beside the dedicated card, each with its maker (and memory, if it has its own).
        foreach (var gpu in info.Gpus.OrderByDescending(gpu => gpu.Integrated))
        {
            var detail = string.Join(" · ", new[]
            {
                SystemInfoText.Vendor(gpu.VendorId),
                gpu.Integrated || gpu.MemoryBytes is not { } bytes ? null : SystemInfoText.Size(bytes, culture)
            }.Where(part => part is not null));

            cards.Add(new BentoCardSpec(gpu.Integrated ? "Integrated GPU" : "GPU", SystemInfoText.GpuName(gpu.Name), detail.Length > 0 ? detail : null));
        }

        if (SystemInfoReader.ReadMemory() is { TotalBytes: > 0 } memory)
        {
            var used = memory.TotalBytes - memory.AvailableBytes;
            var kind = string.Join(" · ", new[] { info.MemoryType, info.MemoryMhz is { } mhz ? $"{mhz} MHz" : null }.Where(part => part is not null));

            cards.Add(new BentoCardSpec(
                "RAM",
                L.F("{0} of {1}", SystemInfoText.Size(used, culture), SystemInfoText.Size(memory.TotalBytes, culture)),
                kind.Length > 0 ? kind : null,
                Bar: used / (double)memory.TotalBytes,
                BarColor: RazerGreen));
        }

        if (info.BiosVersion is { } bios)
        {
            var detail = string.Join(" · ", new[] { info.BiosVendor, SystemInfoText.BiosDate(info.BiosDate, culture) }.Where(part => part is not null));
            cards.Add(new BentoCardSpec("BIOS", bios, detail.Length > 0 ? detail : null));
        }

        // Each drive letter on a physical drive (cloud and virtual drives are
        // left out), captioned with the drive it is on: "C: · WDC WDS500G2B0C (NVMe SSD)".
        foreach (var volume in SystemInfoReader.ReadVolumes())
        {
            if (!info.DriveDisks.TryGetValue(volume.Name, out var diskId))
                continue;

            var disk = info.Disks.FirstOrDefault(candidate => candidate.Id == diskId);
            var kind = disk is null ? null : disk.Bus is "NVMe" or "USB" ? $"{disk.Bus} {disk.Kind}".Trim() : disk.Kind ?? disk.Bus;
            var caption = disk is null
                ? volume.Name
                : kind is null ? $"{volume.Name} · {disk.Model}" : $"{volume.Name} · {disk.Model} ({kind})";
            var used = volume.TotalBytes - volume.FreeBytes;

            cards.Add(new BentoCardSpec(
                caption,
                L.F("{0} of {1}", SystemInfoText.Size(used, culture), SystemInfoText.Size(volume.TotalBytes, culture)),
                L.F("{0} free", SystemInfoText.Size(volume.FreeBytes, culture)),
                Wide: true,
                Bar: volume.TotalBytes > 0 ? used / (double)volume.TotalBytes : null,
                BarColor: RazerGreen));
        }

        return cards;
    }
}
