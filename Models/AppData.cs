namespace TimerApp.Models;

/// <summary>Everything Stint persists, saved as one JSON document.</summary>
public class AppData
{
    public int Version { get; set; } = 1;
    public List<Session> Sessions { get; set; } = [];
    public List<Subject> Subjects { get; set; } = [];
    public AppSettings Settings { get; set; } = new();

    public static AppData CreateDefault()
    {
        var data = new AppData();
        data.EnsureGeneral();
        return data;
    }

    /// <summary>
    /// Repairs anything a hand edit could break: null lists/entries/strings, missing General,
    /// and settings outside the ranges the Settings page allows.
    /// </summary>
    public void EnsureGeneral()
    {
        Sessions = (Sessions ?? []).Where(s => s != null).ToList();
        Subjects = (Subjects ?? []).Where(s => s != null).ToList();
        Settings ??= new();

        foreach (var s in Sessions)
        {
            s.Description ??= "";
            s.Note ??= "";
            s.Seconds = Math.Max(0, s.Seconds);
            s.Rating = Math.Clamp(s.Rating, 0, 5);
        }
        foreach (var s in Subjects)
        {
            if (string.IsNullOrWhiteSpace(s.Name)) s.Name = "Untitled";
            if (string.IsNullOrWhiteSpace(s.Color)) s.Color = "#C8CDD8";
        }
        if (!Subjects.Any(s => s.Id == Subject.GeneralId))
            Subjects.Insert(0, new Subject { Id = Subject.GeneralId, Name = "General", Color = "#C8CDD8" });

        var st = Settings;
        st.FocusMinutes = Math.Clamp(st.FocusMinutes, 1, 180);
        st.ShortBreakMinutes = Math.Clamp(st.ShortBreakMinutes, 1, 60);
        st.LongBreakMinutes = Math.Clamp(st.LongBreakMinutes, 1, 90);
        st.LongBreakEvery = Math.Clamp(st.LongBreakEvery, 1, 12);
        st.DailyGoalMinutes = Math.Clamp(st.DailyGoalMinutes, 5, 1440);
        st.TimerSeconds = Math.Clamp(st.TimerSeconds, 0, 99 * 3600 + 59 * 60 + 59);
    }

    public Subject SubjectFor(Guid id) =>
        Subjects.FirstOrDefault(s => s.Id == id) ?? Subjects.First(s => s.Id == Subject.GeneralId);

    /// <summary>Removes a subject. Its sessions keep their id and resolve to General.</summary>
    public bool DeleteSubject(Guid id)
    {
        if (id == Subject.GeneralId) return false;
        if (Settings.LastSubjectId == id) Settings.LastSubjectId = Subject.GeneralId;
        return Subjects.RemoveAll(s => s.Id == id) > 0;
    }
}
