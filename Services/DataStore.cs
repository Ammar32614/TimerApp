using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TimerApp.Models;

namespace TimerApp.Services;

/// <summary>Loads and saves <see cref="AppData"/> as JSON. Never throws on a bad file.</summary>
public class DataStore(string path)
{
    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Stint", "data.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath => path;

    public AppData Load()
    {
        ReadFailed = false;
        if (!File.Exists(path)) return AppData.CreateDefault();

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked by a sync tool/antivirus etc. Run on defaults but never overwrite the real file.
            ReadFailed = true;
            return AppData.CreateDefault();
        }

        try
        {
            var data = JsonSerializer.Deserialize<AppData>(json, Json) ?? AppData.CreateDefault();
            data.EnsureGeneral();
            return data;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Keep the unreadable file so nothing is silently lost, then start fresh.
            try
            {
                var backup = Path.Combine(Path.GetDirectoryName(path)!, $"data.corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
                File.Copy(path, backup, overwrite: true);
            }
            catch (Exception copyEx) when (copyEx is IOException or UnauthorizedAccessException)
            {
                ReadFailed = true; // couldn't back it up: don't replace it either
            }
            return AppData.CreateDefault();
        }
    }

    /// <summary>True when the last Load couldn't read (or back up) the file; Save then refuses to overwrite it.</summary>
    public bool ReadFailed { get; private set; }

    /// <summary>Writes atomically. Returns false (and writes nothing) if the existing file was never read.</summary>
    public bool Save(AppData data)
    {
        if (ReadFailed) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json));
        File.Move(tmp, path, overwrite: true);
        return true;
    }
}
