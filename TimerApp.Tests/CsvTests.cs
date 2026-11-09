using TimerApp.Models;
using TimerApp.Services;
using Xunit;

namespace TimerApp.Tests;

public class CsvTests
{
    [Fact]
    public void ToCsv_EscapesCommaQuoteNewline()
    {
        var data = AppData.CreateDefault();
        var s = new Session
        {
            Start = new DateTime(2026, 11, 10, 9, 5, 0), Seconds = 1500, Mode = SessionMode.Focus,
            Description = "a,\"b\"\nc", Note = "plain", Rating = 3
        };

        var lines = CsvExporter.ToCsv([s], data).Split("\r\n");

        Assert.Equal("Date,Start,Minutes,Mode,Subject,Description,Note,Rating", lines[0]);
        Assert.Equal("2026-11-10,09:05,25,Focus,General,\"a,\"\"b\"\"\nc\",plain,3", lines[1]);
    }
}
