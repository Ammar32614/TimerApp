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
        if (!File.Exists(path)) return AppData.CreateDefault();
        try
        {
            var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), Json) ?? AppData.CreateDefault();
            data.EnsureGeneral();
            return data;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Keep the unreadable file so nothing is silently lost, then start fresh.
            var backup = Path.Combine(Path.GetDirectoryName(path)!, $"data.corrupt-{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Copy(path, backup, overwrite: true);
            return AppData.CreateDefault();
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
