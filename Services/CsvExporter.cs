using System.Globalization;
using System.Text;
using TimerApp.Models;

namespace TimerApp.Services;

public static class CsvExporter
{
    public const string Header = "Date,Start,Minutes,Mode,Subject,Description,Note,Rating";

    public static string ToCsv(IEnumerable<Session> sessions, AppData data)
    {
        var sb = new StringBuilder(Header);
        foreach (var s in sessions.OrderBy(s => s.Start))
        {
            sb.Append("\r\n").AppendJoin(',',
                s.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                s.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                Math.Round(s.Seconds / 60.0).ToString(CultureInfo.InvariantCulture),
                s.Mode.ToString(),
                Escape(data.SubjectFor(s.SubjectId).Name),
                Escape(s.Description),
                Escape(s.Note),
                s.Rating == 0 ? "" : s.Rating.ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static string Escape(string v) =>
        v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
}
