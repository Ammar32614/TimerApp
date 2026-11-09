using System.Globalization;
using TimerApp.Services;
using Xunit;

namespace TimerApp.Tests;

public class ThemeCatalogTests
{
    [Fact]
    public void All_HasSixUniquelyNamedThemes_MidnightFirst()
    {
        Assert.Equal(["Midnight", "Daylight", "Paper", "Forest", "Ocean", "Dusk"], ThemeCatalog.All.Select(t => t.Name));
    }

    [Fact]
    public void Get_UnknownOrNull_FallsBackToMidnight()
    {
        Assert.Equal("Midnight", ThemeCatalog.Get("Neon").Name);
        Assert.Equal("Midnight", ThemeCatalog.Get(null).Name);
        Assert.Equal("Ocean", ThemeCatalog.Get("ocean").Name); // case-insensitive
    }

    [Fact]
    public void EveryThemeColour_IsValidHex()
    {
        foreach (var t in ThemeCatalog.All)
            foreach (var (_, hex) in t.Colours)
                Assert.Matches("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", hex);
    }

    [Fact]
    public void LightThemes_AreFlagged()
    {
        Assert.True(ThemeCatalog.Get("Daylight").IsLight);
        Assert.True(ThemeCatalog.Get("Paper").IsLight);
        Assert.False(ThemeCatalog.Get("Midnight").IsLight);
    }
}

public class FormatTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(1, "<1m")]
    [InlineData(59, "<1m")]
    [InlineData(60, "1m")]
    [InlineData(45 * 60, "45m")]
    [InlineData(3600, "1h")]
    [InlineData(3900, "1h 5m")]
    public void Duration(int seconds, string expected) => Assert.Equal(expected, Format.Duration(seconds));

    [Theory]
    [InlineData(1, "1 sec")]
    [InlineData(45, "45 sec")]
    [InlineData(60, "1 min")]
    [InlineData(89, "1 min")]
    [InlineData(12 * 60 + 40, "13 min")]
    [InlineData(3900, "1h 5m")]
    public void Spoken(int seconds, string expected) => Assert.Equal(expected, Format.Spoken(seconds));
}

public class SoundLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "stint-sounds-" + Guid.NewGuid().ToString("N"));
    private string Sounds => Path.Combine(_root, "sounds");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private string MakeSource(string name)
    {
        Directory.CreateDirectory(_root);
        var p = Path.Combine(_root, name);
        File.WriteAllBytes(p, [1, 2, 3]);
        return p;
    }

    [Fact]
    public void Import_CopiesIntoSoundsFolder_KeepingExtension()
    {
        var stored = SoundLibrary.Import(MakeSource("bell.MP3"), Sounds);

        Assert.StartsWith(Sounds, stored);
        Assert.Equal(".mp3", Path.GetExtension(stored).ToLower(CultureInfo.InvariantCulture));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(stored));
    }

    [Fact]
    public void Import_Again_RemovesThePreviousCopy()
    {
        var first = SoundLibrary.Import(MakeSource("a.wav"), Sounds);
        var second = SoundLibrary.Import(MakeSource("b.wav"), Sounds);

        Assert.False(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.Single(Directory.GetFiles(Sounds));
    }

    [Fact]
    public void Import_UnsupportedType_Throws()
    {
        Assert.Throws<ArgumentException>(() => SoundLibrary.Import(MakeSource("song.flac"), Sounds));
    }
}
