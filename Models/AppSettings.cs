namespace TimerApp.Models;

public class AppSettings
{
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int LongBreakEvery { get; set; } = 4;
    public bool AutoStartBreaks { get; set; }
    public bool AutoStartFocus { get; set; }
    public int DailyGoalMinutes { get; set; } = 120;
    public bool Topmost { get; set; }
    public bool SoundOn { get; set; } = true;
    public int TimerSeconds { get; set; } = 600;
    public Guid LastSubjectId { get; set; } = Subject.GeneralId;
    public string Theme { get; set; } = "Midnight";
    /// <summary>Stint's own copy of a custom alarm (null = Windows default alarm).</summary>
    public string? AlarmSoundPath { get; set; }
    /// <summary>The file name the user picked, for display.</summary>
    public string? AlarmSoundName { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
}
