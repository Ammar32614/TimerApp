using TimerApp.Models;
using TimerApp.Services;
using Xunit;

namespace TimerApp.Tests;

public class FocusCycleTests
{
    [Fact]
    public void Advance_FourFocus_GivesLongBreak()
    {
        var c = new FocusCycle();
        var seen = new List<Phase> { c.Current };
        for (int i = 0; i < 8; i++) { c.Advance(4); seen.Add(c.Current); }

        Assert.Equal(new[]
        {
            Phase.Focus, Phase.ShortBreak, Phase.Focus, Phase.ShortBreak, Phase.Focus,
            Phase.ShortBreak, Phase.Focus, Phase.LongBreak, Phase.Focus
        }, seen);
        Assert.Equal(4, c.CompletedFocus);
    }

    [Fact]
    public void DotIndex_TracksSlotWithinCycle()
    {
        var c = new FocusCycle();
        Assert.Equal(0, c.DotIndex(4));
        c.Advance(4); // short break after 1st focus
        Assert.Equal(0, c.DotIndex(4));
        c.Advance(4); // 2nd focus
        Assert.Equal(1, c.DotIndex(4));
    }

    [Fact]
    public void Restore_PutsCycleBackExactly()
    {
        var c = new FocusCycle();
        c.Advance(4); c.Advance(4); c.Advance(4); // focus #2 done -> short break
        var (phase, done) = (c.Current, c.CompletedFocus);
        c.Reset();

        c.Restore(phase, done);

        Assert.Equal(Phase.ShortBreak, c.Current);
        Assert.Equal(2, c.CompletedFocus);
    }

    [Fact]
    public void DurationOf_UsesSettings()
    {
        var s = new AppSettings { FocusMinutes = 50, ShortBreakMinutes = 10, LongBreakMinutes = 30 };
        Assert.Equal(TimeSpan.FromMinutes(50), FocusCycle.DurationOf(Phase.Focus, s));
        Assert.Equal(TimeSpan.FromMinutes(10), FocusCycle.DurationOf(Phase.ShortBreak, s));
        Assert.Equal(TimeSpan.FromMinutes(30), FocusCycle.DurationOf(Phase.LongBreak, s));
    }

    [Fact]
    public void ShouldLog_59s_False_60s_True()
    {
        Assert.False(SessionRules.ShouldLog(TimeSpan.FromSeconds(59)));
        Assert.True(SessionRules.ShouldLog(TimeSpan.FromSeconds(60)));
    }
}
