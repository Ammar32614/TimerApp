using TimerApp.Models;

namespace TimerApp.Services;

public enum Phase { Focus, ShortBreak, LongBreak }

/// <summary>Pomodoro progression: focus, short break, …, long break every N focus blocks.</summary>
public class FocusCycle
{
    public Phase Current { get; private set; } = Phase.Focus;
    public int CompletedFocus { get; private set; }

    public void Advance(int longBreakEvery)
    {
        longBreakEvery = Math.Max(1, longBreakEvery);
        if (Current == Phase.Focus)
        {
            CompletedFocus++;
            Current = CompletedFocus % longBreakEvery == 0 ? Phase.LongBreak : Phase.ShortBreak;
        }
        else
        {
            Current = Phase.Focus;
        }
    }

    public void Reset()
    {
        Current = Phase.Focus;
        CompletedFocus = 0;
    }

    public static TimeSpan DurationOf(Phase p, AppSettings s) => TimeSpan.FromMinutes(p switch
    {
        Phase.Focus => s.FocusMinutes,
        Phase.ShortBreak => s.ShortBreakMinutes,
        _ => s.LongBreakMinutes,
    });

    /// <summary>0-based focus slot within the current cycle (breaks show the slot just finished).</summary>
    public int DotIndex(int longBreakEvery)
    {
        longBreakEvery = Math.Max(1, longBreakEvery);
        return Current == Phase.Focus
            ? CompletedFocus % longBreakEvery
            : (CompletedFocus - 1) % longBreakEvery;
    }
}

public static class SessionRules
{
    public static readonly TimeSpan MinimumLogged = TimeSpan.FromMinutes(1);
    public static bool ShouldLog(TimeSpan elapsed) => elapsed >= MinimumLogged;
}
