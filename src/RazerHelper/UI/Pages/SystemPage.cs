using RazerHelper.Core.Hardware;

namespace RazerHelper.UI.Pages;

/// <summary>The laptop's parts and Windows, as cards (see <see cref="SystemInfoView"/>).</summary>
internal sealed class SystemPage : PageView
{
    private readonly SystemInfoView _info;

    public SystemPage(Func<SystemInfo> read)
        : base("System")
    {
        _info = new SystemInfoView(read);
        Add(_info);
    }

    public override void OnPageShown() => _info.Start();

    public override void OnPageHidden() => _info.Stop();
}
