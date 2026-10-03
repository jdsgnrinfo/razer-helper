using System.Globalization;
using RazerHelper.Core.Diagnostics;
using RazerHelper.Core.Hardware;
using RazerHelper.Core.Localization;
using RazerHelper.Core.Services;
using static RazerHelper.UI.UiTheme;

namespace RazerHelper.UI.Pages;

/// <summary>
/// The System page's figures: the laptop model and its maker across the
/// top, then two to a row Windows and the processor, the graphics, memory
/// (with a bar of how full it is) and the BIOS, and each drive letter with
/// its drive and a bar. The fixed figures are read once in the background,
/// the first time the page is shown; memory and drive space follow along
/// while it is on screen.
/// </summary>
internal sealed class SystemInfoView(Func<SystemInfo> read) : DetailsView("System information", semiBoldValues: true)
{
    private Task<SystemInfo>? _reading;
    private bool _loggedFailure;

    private Task<SystemInfo> Info => _reading ??= ReadInBackground();

    public override void Start()
    {
        _ = Info;
        base.Start();
    }

    private Task<SystemInfo> ReadInBackground()
    {
        var reading = Task.Run(read);

        // Shown as soon as it is read, rather than at the next refresh.
        reading.ContinueWith(
            _ =>
            {
                try
                {
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke(Refresh);
                }
                catch (InvalidOperationException)
                {
                    // Closed meanwhile: nothing left to show it in.
                }
            },
            TaskScheduler.Default);

        return reading;
    }

    protected override bool IsLoading => !Info.IsCompleted;

    protected override (string Header, IReadOnlyList<BentoCardSpec> Cards) ReadCards()
    {
        var header = L.T("System information");

        if (!Info.IsCompleted)
            return (header, [new BentoCardSpec("System information", L.T("Reading..."), null, Wide: true)]);

        if (!Info.IsCompletedSuccessfully)
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                AppLog.Error("Could not read the system information.", Info.Exception);
            }

            return (header, [new BentoCardSpec("System information", L.T("No information"), L.T("Windows did not report these figures"), Wide: true)]);
        }

        return (header, CardsFor(Info.Result, CultureInfo.CurrentCulture));
    }

    private static List<BentoCardSpec> CardsFor(SystemInfo info, CultureInfo culture)
    {
        var cards = new List<BentoCardSpec>();

        // The model first, across the whole width, with its maker under it.
        if (info.Model is { } model)
            cards.Add(new BentoCardSpec("Model", SystemInfoText.ModelName(model), info.Manufacturer, Wide: true));

        if (info.OsName is { } os)
            cards.Add(new BentoCardSpec("Operating system", SystemInfoText.OsName(os, info.OsBuild), SystemInfoText.OsDetail(info.OsVersion, info.OsBuild, info.OsRevision)));

        if (info.CpuName is { } cpu)
            cards.Add(new BentoCardSpec("CPU", SystemInfoText.CpuName(cpu), SystemInfoText.CpuDetail(info.CpuCores, info.CpuThreads)));

        // The built-in graphics beside the dedicated card, each with its maker and memory: "Intel (128 MB)".
        foreach (var gpu in info.Gpus.OrderByDescending(gpu => gpu.Integrated))
        {
            var detail = SystemInfoText.GpuDetail(SystemInfoText.Vendor(gpu.VendorId), gpu.MemoryBytes, culture);
            cards.Add(new BentoCardSpec(gpu.Integrated ? "Integrated GPU" : "GPU", SystemInfoText.GpuName(gpu.Name), detail));
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
