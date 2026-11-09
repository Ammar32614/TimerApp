using System.IO;

namespace TimerApp.Services;

/// <summary>Keeps Stint's own copy of the user's chosen alarm sound.</summary>
public static class SoundLibrary
{
    public static readonly string[] Extensions = [".wav", ".mp3", ".wma"];
    public const string DialogFilter = "Sound files (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma";

    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stint", "sounds");

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Copies <paramref name="source"/> into <paramref name="folder"/>, replacing any previous copy.</summary>
    /// <returns>The path of the stored copy.</returns>
    public static string Import(string source, string folder)
    {
        if (!IsSupported(source))
            throw new ArgumentException("Choose a WAV, MP3 or WMA file.", nameof(source));

        Directory.CreateDirectory(folder);
        var ext = Path.GetExtension(source).ToLowerInvariant();
        var dest = Path.Combine(folder, $"alarm-{DateTime.Now:yyyyMMddHHmmssfff}{ext}");
        File.Copy(source, dest, overwrite: true);

        foreach (var old in Directory.GetFiles(folder, "alarm-*"))
            if (!string.Equals(old, dest, StringComparison.OrdinalIgnoreCase))
                TryDelete(old);
        return dest;
    }

    public static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
