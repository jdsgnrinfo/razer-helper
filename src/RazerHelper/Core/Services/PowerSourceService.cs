using Microsoft.Win32;

namespace RazerHelper.Core.Services;

internal sealed class PowerSourceService : IPowerSource, IDisposable
{
    private readonly Func<bool?> _readPluggedIn;
    private readonly object _sync = new();
    private bool? _lastPluggedIn;

    public PowerSourceService() : this(ReadWindowsPowerLine)
    {
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
    }

    // The reader is a parameter so the filtering can be tested without Windows.
    internal PowerSourceService(Func<bool?> readPluggedIn)
    {
        _readPluggedIn = readPluggedIn;
        _lastPluggedIn = readPluggedIn();
    }

    public bool? IsPluggedIn => _readPluggedIn();

    // Windows reports a fraction, or 255 when there is no battery or it cannot tell.
    public int? BatteryPercent =>
        SystemInformation.PowerStatus.BatteryLifePercent is var fraction and >= 0f and <= 1f
            ? (int)Math.Round(fraction * 100)
            : null;

    public event EventHandler? PowerSourceChanged;

    private void SystemEvents_PowerModeChanged(
        object? sender,
        PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange)
            OnStatusChange();
    }

    /// <summary>
    /// Windows reports a status change for every battery percentage step as well
    /// as for plugging in and unplugging. Only the latter matters to anyone
    /// listening, so the rest is dropped here instead of waking every listener.
    /// </summary>
    internal void OnStatusChange()
    {
        var pluggedIn = _readPluggedIn();

        lock (_sync)
        {
            if (pluggedIn == _lastPluggedIn)
                return;

            _lastPluggedIn = pluggedIn;
        }

        PowerSourceChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool? ReadWindowsPowerLine() => SystemInformation.PowerStatus.PowerLineStatus switch
    {
        PowerLineStatus.Online => true,
        PowerLineStatus.Offline => false,
        _ => null
    };

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
    }
}
