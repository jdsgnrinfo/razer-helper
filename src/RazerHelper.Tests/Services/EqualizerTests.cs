using RazerHelper.Core.Services;

namespace RazerHelper.Tests.Services;

public class EqualizerTests
{
    private const string Speakers = "{0.0.0.00000000}.{AFAD2450-A064-4347-82D4-D9DCEBB60CDD}";
    private const string Headphones = "{0.0.0.00000000}.{a176a49e-5310-4a2e-96b4-16940d8e8691}";

    [Fact]
    public void TheDeviceIdIsTheEndpointsLastPart_InLowerCase() =>
        Assert.Equal("{afad2450-a064-4347-82d4-d9dcebb60cdd}", EqualizerApoConfig.DeviceGuid(Speakers));

    [Fact]
    public void EachDeviceThatIsOn_GetsItsOwnSection_WithItsPreampAndTenBands()
    {
        var text = EqualizerApoConfig.Build(new Dictionary<string, AudioDeviceEq>
        {
            [Speakers] = new(true, "Bass boost", -6, [6, 5.5, 4, 2, 0, -1, 0, 1.5, 2.5, 3])
        });

        Assert.StartsWith(EqualizerApoConfig.Marker, text);
        Assert.Contains("Device: {afad2450-a064-4347-82d4-d9dcebb60cdd}", text);
        Assert.Contains("Preamp: -6 dB", text);
        Assert.Contains("GraphicEQ: 31 6; 62 5.5; 125 4; 250 2; 500 0; 1000 -1; 2000 0; 4000 1.5; 8000 2.5; 16000 3", text);
    }

    [Fact]
    public void ADeviceThatIsOff_IsLeftOut_SoItPlaysAsItIs()
    {
        var text = EqualizerApoConfig.Build(new Dictionary<string, AudioDeviceEq>
        {
            [Speakers] = new(false, "Bass boost", -6, [6, 5, 4, 2, 0, 0, 0, 0, 0, 0]),
            [Headphones] = AudioDeviceEq.Default
        });

        Assert.DoesNotContain("afad2450", text);
        Assert.Contains("Device: {a176a49e-5310-4a2e-96b4-16940d8e8691}", text);
        Assert.Single(text.Split('\n'), line => line.StartsWith("Device:", StringComparison.Ordinal));
    }

    [Fact]
    public void NothingOn_WritesNoCommands()
    {
        var text = EqualizerApoConfig.Build([]);

        Assert.All(text.Split('\n', StringSplitOptions.RemoveEmptyEntries), line => Assert.StartsWith("#", line));
    }

    [Fact]
    public void ACurveFromAHandEditedFile_IsKeptToTheBands()
    {
        var curve = new AudioDeviceEq(true, null, -40, [30, -30, 1.26]).Normalized();

        Assert.Equal(EqBands.PreampMinimum, curve.Preamp);
        Assert.Equal([12, -12, 1.5, 0, 0, 0, 0, 0, 0, 0], curve.Gains);
    }

    [Fact]
    public void NumbersAreWrittenWithAPoint_WhateverTheLanguage()
    {
        var before = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");

        try
        {
            var text = EqualizerApoConfig.Build(new Dictionary<string, AudioDeviceEq> { [Speakers] = new(true, null, -3, [5.5, 0, 0, 0, 0, 0, 0, 0, 0, 0]) });
            Assert.Contains("31 5.5;", text);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = before;
        }
    }

    [Fact]
    public void EveryBuiltInPreset_HasTenBands_AndAPreampThatLeavesRoomForItsBoost()
    {
        Assert.All(EqPreset.BuiltIn, preset =>
        {
            Assert.Equal(EqBands.Count, preset.Gains.Length);
            Assert.True(preset.Preamp <= -preset.Gains.Max() + 0.001 || preset.Gains.Max() <= 0, preset.Name);
        });
    }

    [Fact]
    public void ACurveMatchesItsPreset_UntilABandMoves()
    {
        var bass = EqPreset.BuiltIn[1];
        var device = AudioDeviceEq.Default.WithPreset(bass);

        Assert.True(bass.Matches(device));
        Assert.False(bass.Matches(device with { Gains = [.. device.Gains.Select((gain, band) => band == 0 ? gain - 1 : gain)] }));
    }

    [Fact]
    public void TheFirstWrite_KeepsTheFileItFound_AndLaterWritesLeaveThatCopyAlone()
    {
        var folder = Directory.CreateTempSubdirectory("rh-eq-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(folder, "config.txt"), "Preamp: -6 dB\nInclude: example.txt\n");
            var writer = new EqualizerApoWriter(folder);

            writer.Write(EqualizerApoConfig.Marker + "\nfirst\n");
            writer.Write(EqualizerApoConfig.Marker + "\nsecond\n");

            Assert.Equal("Preamp: -6 dB\nInclude: example.txt\n", File.ReadAllText(Path.Combine(folder, EqualizerApoWriter.BackupName)));
            Assert.Equal(EqualizerApoConfig.Marker + "\nsecond\n", File.ReadAllText(Path.Combine(folder, "config.txt")));
            Assert.False(File.Exists(Path.Combine(folder, "config.txt.tmp")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
