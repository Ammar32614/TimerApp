using TimerApp.Models;
using TimerApp.Services;
using Xunit;

namespace TimerApp.Tests;

public class StatsTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static Session At(DateOnly day, int hour, int seconds, Guid? subject = null) => new()
    {
        Start = day.ToDateTime(new TimeOnly(hour, 0)),
        Seconds = seconds,
        SubjectId = subject ?? Subject.GeneralId,
    };

    [Fact]
    public void Streak_TodayAndYesterday_Is2() =>
        Assert.Equal(2, StatsCalculator.Streak([At(Today, 9, 600), At(Today.AddDays(-1), 9, 600)], Today));

    [Fact]
    public void Streak_OnlyYesterday_Is1() =>
        Assert.Equal(1, StatsCalculator.Streak([At(Today.AddDays(-1), 9, 600)], Today));

    [Fact]
    public void Streak_GapTwoDaysAgo_Is0WhenNothingTodayOrYesterday() =>
        Assert.Equal(0, StatsCalculator.Streak([At(Today.AddDays(-2), 9, 600)], Today));

    [Fact]
    public void SecondsOn_SessionCrossingMidnight_CountsOnStartDay()
    {
        var s = new Session { Start = Today.AddDays(-1).ToDateTime(new TimeOnly(23, 50)), Seconds = 1200 };
        Assert.Equal(1200, StatsCalculator.SecondsOn([s], Today.AddDays(-1)));
        Assert.Equal(0, StatsCalculator.SecondsOn([s], Today));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(60, 1)]
    [InlineData(30 * 60, 2)]
    [InlineData(60 * 60, 3)]
    [InlineData(120 * 60, 4)]
    public void Level_Buckets(int seconds, int expected) =>
        Assert.Equal(expected, StatsCalculator.Level(seconds, 120));

    [Fact]
    public void HeatLevels_Returns84_LastIsToday()
    {
        var levels = StatsCalculator.HeatLevels([At(Today, 9, 120 * 60)], Today, 120);
        Assert.Equal(84, levels.Length);
        Assert.Equal(4, levels[^1]);
        Assert.Equal(0, levels[^2]);
    }

    [Fact]
    public void Week_WithResolver_MergesDeletedSubjectsIntoGeneral()
    {
        var gone1 = Guid.NewGuid();
        var gone2 = Guid.NewGuid();
        var week = StatsCalculator.Week(
            [At(Today, 9, 600, gone1), At(Today, 11, 300, gone2), At(Today, 13, 100)],
            Today,
            _ => Subject.GeneralId);

        Assert.Single(week[^1].BySubject);
        Assert.Equal(1000, week[^1].BySubject[Subject.GeneralId]);
    }

    [Fact]
    public void Week_Returns7Days_EndingLastDay_GroupedBySubject()
    {
        var math = Guid.NewGuid();
        var week = StatsCalculator.Week(
            [At(Today, 9, 600, math), At(Today, 11, 300, math), At(Today, 13, 100), At(Today.AddDays(-6), 9, 50)],
            Today);

        Assert.Equal(7, week.Count);
        Assert.Equal(Today, week[^1].Day);
        Assert.Equal(Today.AddDays(-6), week[0].Day);
        Assert.Equal(900, week[^1].BySubject[math]);
        Assert.Equal(100, week[^1].BySubject[Subject.GeneralId]);
        Assert.Equal(50, week[0].BySubject[Subject.GeneralId]);
    }
}
