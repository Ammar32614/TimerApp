using TimerApp.Models;

namespace TimerApp.Services;

/// <summary>Aggregations over the study log. A session belongs to the day it started.</summary>
public static class StatsCalculator
{
    private static DateOnly DayOf(Session s) => DateOnly.FromDateTime(s.Start);

    public static int SecondsOn(IEnumerable<Session> sessions, DateOnly day) =>
        sessions.Where(s => DayOf(s) == day).Sum(s => s.Seconds);

    /// <summary>Consecutive days with any study, ending today (or yesterday if today is still empty).</summary>
    public static int Streak(IEnumerable<Session> sessions, DateOnly today)
    {
        var days = sessions.Where(s => s.Seconds > 0).Select(DayOf).ToHashSet();
        var day = days.Contains(today) ? today : today.AddDays(-1);
        int streak = 0;
        while (days.Contains(day)) { streak++; day = day.AddDays(-1); }
        return streak;
    }

    public static IReadOnlyList<(DateOnly Day, Dictionary<Guid, int> BySubject)> Week(
        IEnumerable<Session> sessions, DateOnly lastDay)
    {
        var first = lastDay.AddDays(-6);
        var list = Enumerable.Range(0, 7)
            .Select(i => (Day: first.AddDays(i), BySubject: new Dictionary<Guid, int>()))
            .ToList();
        foreach (var s in sessions)
        {
            var d = DayOf(s);
            if (d < first || d > lastDay) continue;
            var bucket = list[d.DayNumber - first.DayNumber].BySubject;
            bucket[s.SubjectId] = bucket.GetValueOrDefault(s.SubjectId) + s.Seconds;
        }
        return list;
    }

    /// <summary>Intensity 0–4 for <paramref name="days"/> days ending today, oldest first.</summary>
    public static int[] HeatLevels(IEnumerable<Session> sessions, DateOnly today, int goalMinutes, int days = 84)
    {
        var totals = sessions.GroupBy(DayOf).ToDictionary(g => g.Key, g => g.Sum(s => s.Seconds));
        var first = today.AddDays(-(days - 1));
        return Enumerable.Range(0, days)
            .Select(i => Level(totals.GetValueOrDefault(first.AddDays(i)), goalMinutes))
            .ToArray();
    }

    public static int Level(int seconds, int goalMinutes)
    {
        if (seconds <= 0) return 0;
        double ratio = seconds / (Math.Max(1, goalMinutes) * 60.0);
        return ratio < 0.25 ? 1 : ratio < 0.5 ? 2 : ratio < 1 ? 3 : 4;
    }
}
