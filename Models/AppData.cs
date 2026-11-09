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

    /// <summary>Guarantees the undeletable "General" subject exists (e.g. after a hand edit).</summary>
    public void EnsureGeneral()
    {
        Sessions ??= [];
        Subjects ??= [];
        Settings ??= new();
        if (!Subjects.Any(s => s.Id == Subject.GeneralId))
            Subjects.Insert(0, new Subject { Id = Subject.GeneralId, Name = "General", Color = "#C8CDD8" });
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
