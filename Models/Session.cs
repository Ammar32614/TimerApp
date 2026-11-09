namespace TimerApp.Models;

public enum SessionMode { Focus, Timer, Stopwatch }

/// <summary>One block of logged study time.</summary>
public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Local start time. A session counts towards the day it started on.</summary>
    public DateTime Start { get; set; }
    public int Seconds { get; set; }
    public SessionMode Mode { get; set; }
    public Guid SubjectId { get; set; } = Subject.GeneralId;
    public string Description { get; set; } = "";
    public string Note { get; set; } = "";
    /// <summary>0 = not rated, otherwise 1–5.</summary>
    public int Rating { get; set; }

    public DateTime End => Start.AddSeconds(Seconds);
}
