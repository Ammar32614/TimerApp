namespace TimerApp.Services;

/// <summary>Human-readable durations used across the app.</summary>
public static class Format
{
    /// <summary>Compact form for lists and stats: "0m", "&lt;1m", "45m", "1h", "1h 5m".</summary>
    public static string Duration(int seconds)
    {
        if (seconds <= 0) return "0m";
        if (seconds < 60) return "<1m";
        int m = seconds / 60;
        return m < 60 ? $"{m}m" : m % 60 == 0 ? $"{m / 60}h" : $"{m / 60}h {m % 60}m";
    }

    /// <summary>Sentence form for buttons and messages: "45 sec", "13 min", "1h 5m".</summary>
    public static string Spoken(int seconds)
    {
        if (seconds < 60) return $"{Math.Max(1, seconds)} sec";
        int m = (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero);
        if (seconds < 90) m = 1;
        return m < 60 ? $"{m} min" : Duration(m * 60);
    }
}
