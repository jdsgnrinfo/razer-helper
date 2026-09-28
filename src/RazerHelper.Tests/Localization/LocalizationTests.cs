using System.Globalization;
using System.Text.RegularExpressions;
using RazerHelper.Core.Localization;

namespace RazerHelper.Tests.Localization;

public partial class LocalizationTests
{
    // L.T("...") or L.F("...", ...) with a single plain literal: the texts that
    // are looked up as they are written.
    [GeneratedRegex("""L\.[TF]\("((?:[^"\\]|\\.)*)"\s*[,)]""")]
    private static partial Regex TranslatedLiteral();

    private static string FindSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var source = Path.Combine(directory.FullName, "src", "RazerHelper");

            if (Directory.Exists(source))
                return source;
        }

        throw new InvalidOperationException("Could not find the app's source from " + AppContext.BaseDirectory);
    }

    private static List<(string Text, string Where)> LiteralsInSource()
    {
        var separator = Path.DirectorySeparatorChar;
        var found = new List<(string, string)>();

        foreach (var path in Directory.EnumerateFiles(FindSourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}obj{separator}") && !path.Contains($"{separator}bin{separator}")))
        {
            foreach (Match match in TranslatedLiteral().Matches(File.ReadAllText(path)))
                found.Add((Regex.Unescape(match.Groups[1].Value), Path.GetFileName(path)));
        }

        return found;
    }

    [Fact]
    public void TheTranslatedTextsAreFound_SoTheCheckBelowIsNotVacuous() =>
        Assert.True(LiteralsInSource().Count > 100);

    [Fact]
    public void EveryTextMarkedForTranslationHasASpanishOne()
    {
        var missing = LiteralsInSource()
            .Where(literal => !L.Spanish.ContainsKey(literal.Text))
            .Select(literal => $"{literal.Where}: \"{literal.Text}\"")
            .Distinct()
            .ToList();

        Assert.True(missing.Count == 0, "No Spanish text for:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EachSpanishTextKeepsTheSameValues()
    {
        var placeholder = new Regex(@"\{\d+\}");

        foreach (var (english, spanish) in L.Spanish)
        {
            var wanted = placeholder.Matches(english).Select(match => match.Value).Order();
            var got = placeholder.Matches(spanish).Select(match => match.Value).Order();

            Assert.True(wanted.SequenceEqual(got), $"\"{spanish}\" does not carry the values of \"{english}\".");
        }
    }

    [Fact]
    public void EnglishShowsTheTextAsWritten()
    {
        Assert.Equal(AppLanguage.English, L.Current);
        Assert.Equal("Battery", L.T("Battery"));
        Assert.Equal("Switched to 120 Hz.", L.F("Switched to {0} Hz.", 120));
    }

    [Theory]
    [InlineData("es", "en-US", true)]
    [InlineData("en", "es-ES", false)]
    [InlineData("ES", "en-US", true)]
    [InlineData(null, "es-MX", true)]
    [InlineData(null, "es-ES", true)]
    [InlineData(null, "en-GB", false)]
    [InlineData(null, "fr-FR", false)]
    [InlineData("klingon", "fr-FR", false)]
    public void TheLanguageIsTheSavedOne_OrElseFollowsWindows(string? saved, string windows, bool spanish) =>
        Assert.Equal(spanish ? AppLanguage.Spanish : AppLanguage.English, L.Resolve(saved, CultureInfo.GetCultureInfo(windows)));

    [Fact]
    public void TheSavedCodeReadsBackAsTheSameLanguage()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
            Assert.Equal(language, L.Resolve(L.Code(language), CultureInfo.InvariantCulture));
    }
}
