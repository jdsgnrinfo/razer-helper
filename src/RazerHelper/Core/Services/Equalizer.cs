using System.Globalization;
using System.Text;

namespace RazerHelper.Core.Services;

/// <summary>The equalizer's ten bands: where each sits and how far it moves.</summary>
internal static class EqBands
{
    /// <summary>Each band's frequency, in Hz, low to high.</summary>
    public static readonly int[] Frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    public static int Count => Frequencies.Length;

    /// <summary>How far a band moves up or down, in dB.</summary>
    public const double Range = 12;

    /// <summary>The smallest move, in dB.</summary>
    public const double Step = 0.5;

    /// <summary>The preamp's lowest setting, in dB; its highest is 0.</summary>
    public const int PreampMinimum = -24;

    /// <summary>A band's frequency as it is written under it: 31, 500, 1k, 16k.</summary>
    public static string Label(int frequency) =>
        frequency >= 1000 ? $"{frequency / 1000}k" : frequency.ToString(CultureInfo.InvariantCulture);

    /// <summary>A gain kept on the band's range, on a whole step.</summary>
    public static double Snap(double gain) =>
        Math.Round(Math.Clamp(gain, -Range, Range) / Step, MidpointRounding.AwayFromZero) * Step;

    public static double[] Flat() => new double[Count];
}

/// <summary>A named curve to start from: one of the app's, or one the user saved.</summary>
internal sealed record EqPreset(string Name, double Preamp, double[] Gains)
{
    /// <summary>The app's own, in the list's order; their names are translated on screen.</summary>
    public static readonly IReadOnlyList<EqPreset> BuiltIn =
    [
        new("Flat", 0, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        new("Bass boost", -6, [6, 5, 4, 2, 0, 0, 0, 0, 0, 0]),
        new("Treble boost", -6, [0, 0, 0, 0, 0, 1, 2, 4, 5, 6]),
        new("Vocal", -4, [-2, -1, 0, 2, 4, 4, 3, 1, 0, -1]),
        new("Gaming", -5, [3, 2, 0, -1, 0, 2, 4, 5, 4, 2]),
        new("Loudness", -6, [6, 4, 1, 0, -1, 0, 0, 2, 4, 5])
    ];

    public static bool IsBuiltIn(string name) =>
        BuiltIn.Any(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a device's curve is exactly this one.</summary>
    public bool Matches(AudioDeviceEq device) =>
        Preamp == device.Preamp && Gains.SequenceEqual(device.Gains);
}

/// <summary>
/// One output device's equalizer: on or off, the preset it came from (if it
/// still has one), and its own curve, which is what plays.
/// </summary>
internal sealed record AudioDeviceEq(bool Enabled, string? Preset, double Preamp, double[] Gains)
{
    /// <summary>A device seen for the first time: on, and flat, so nothing changes until the curve does.</summary>
    public static AudioDeviceEq Default => new(true, "Flat", 0, EqBands.Flat());

    /// <summary>The curve with its gains trimmed to the bands (a hand-edited settings file may hold anything).</summary>
    public AudioDeviceEq Normalized() => this with
    {
        Preamp = Math.Clamp(Math.Round(Preamp), EqBands.PreampMinimum, 0),
        Gains = [.. Enumerable.Range(0, EqBands.Count).Select(band => EqBands.Snap(band < (Gains?.Length ?? 0) ? Gains![band] : 0))]
    };

    public AudioDeviceEq WithPreset(EqPreset preset) =>
        this with { Preset = preset.Name, Preamp = preset.Preamp, Gains = [.. preset.Gains] };
}

/// <summary>
/// The file Equalizer APO plays: a section per output device whose equalizer
/// is on, picked by the device's id, with its preamp and its ten bands. A
/// device that is off, or not in the file, plays as it is.
/// </summary>
internal static class EqualizerApoConfig
{
    /// <summary>The first line of a file the app wrote, which tells it from the user's own.</summary>
    public const string Marker = "# Written by RazerHelper.";

    /// <summary>The id Equalizer APO knows a device by: the last part of Windows' endpoint id, "{0.0.0.00000000}.{guid}".</summary>
    public static string DeviceGuid(string endpointId)
    {
        var dot = endpointId.LastIndexOf('.');
        return (dot >= 0 ? endpointId[(dot + 1)..] : endpointId).ToLowerInvariant();
    }

    public static string Build(IEnumerable<KeyValuePair<string, AudioDeviceEq>> devices)
    {
        var text = new StringBuilder();
        text.AppendLine(Marker);
        text.AppendLine("# Set from the app's Audio page; changes made here are replaced the next time it saves.");

        foreach (var (endpointId, device) in devices.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!device.Enabled)
                continue;

            var curve = device.Normalized();

            text.AppendLine();
            text.AppendLine($"Device: {DeviceGuid(endpointId)}");
            text.AppendLine($"Preamp: {Number(curve.Preamp)} dB");
            text.AppendLine("GraphicEQ: " + string.Join("; ",
                EqBands.Frequencies.Select((frequency, band) => $"{frequency} {Number(curve.Gains[band])}")));
        }

        return text.ToString();
    }

    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}

/// <summary>
/// Writes the equalizer into Equalizer APO's config.txt, which it reads again
/// by itself whenever the file changes. The first time, the file it finds
/// (Equalizer APO's sample, or the user's own) is kept beside it, untouched.
/// </summary>
internal sealed class EqualizerApoWriter(string configFolder)
{
    public const string BackupName = "config.before-RazerHelper.txt";

    private string ConfigPath => Path.Combine(configFolder, "config.txt");

    public void Write(string text)
    {
        var backup = Path.Combine(configFolder, BackupName);

        if (File.Exists(ConfigPath) && !File.Exists(backup) && !File.ReadAllText(ConfigPath).StartsWith(EqualizerApoConfig.Marker, StringComparison.Ordinal))
            File.Copy(ConfigPath, backup);

        // Written beside it and moved in, so Equalizer APO never reads half a file.
        var temporary = ConfigPath + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, ConfigPath, overwrite: true);
    }
}
