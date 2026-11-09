using TimerApp.Models;
using TimerApp.Services;
using Xunit;

namespace TimerApp.Tests;

public class DataStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "stint-tests-" + Guid.NewGuid().ToString("N"));
    private string PathIn => System.IO.Path.Combine(_dir, "data.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultsWithGeneral()
    {
        var data = new DataStore(PathIn).Load();
        Assert.Single(data.Subjects);
        Assert.Equal(Subject.GeneralId, data.Subjects[0].Id);
        Assert.Equal(25, data.Settings.FocusMinutes);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsSession()
    {
        var store = new DataStore(PathIn);
        var data = AppData.CreateDefault();
        var s = new Session
        {
            Id = Guid.NewGuid(), Start = new DateTime(2026, 11, 10, 9, 30, 0), Seconds = 1500,
            Mode = SessionMode.Focus, SubjectId = Subject.GeneralId,
            Description = "Chapter 3", Note = "went well", Rating = 4
        };
        data.Sessions.Add(s);
        store.Save(data);

        var loaded = store.Load().Sessions.Single();
        Assert.Equal(s.Id, loaded.Id);
        Assert.Equal(s.Start, loaded.Start);
        Assert.Equal(1500, loaded.Seconds);
        Assert.Equal(SessionMode.Focus, loaded.Mode);
        Assert.Equal("Chapter 3", loaded.Description);
        Assert.Equal("went well", loaded.Note);
        Assert.Equal(4, loaded.Rating);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndBacksUp()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn, "{not json");

        var data = new DataStore(PathIn).Load();

        Assert.Equal(Subject.GeneralId, data.Subjects.Single().Id);
        Assert.NotEmpty(Directory.GetFiles(_dir, "data.corrupt-*.json"));
    }

    [Fact]
    public void Load_NullSubjectEntry_DoesNotThrow()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn, "{\"subjects\":[null],\"sessions\":[null]}");

        var data = new DataStore(PathIn).Load();

        Assert.Equal(Subject.GeneralId, data.Subjects.Single().Id);
        Assert.Empty(data.Sessions);
    }

    [Fact]
    public void Load_NullStringsAndColor_AreNormalised()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn,
            "{\"subjects\":[{\"id\":\"00000000-0000-0000-0000-000000000002\",\"name\":null,\"color\":null}]," +
            "\"sessions\":[{\"seconds\":600,\"description\":null,\"note\":null}]}");

        var data = new DataStore(PathIn).Load();

        var s = data.Sessions.Single();
        Assert.Equal("", s.Description);
        Assert.Equal("", s.Note);
        var math = data.Subjects.Single(x => x.Id != Subject.GeneralId);
        Assert.False(string.IsNullOrEmpty(math.Color));
        Assert.False(string.IsNullOrEmpty(math.Name));
        Assert.Contains("600", "" + CsvExporter.ToCsv(data.Sessions, data).Length + "600"); // does not throw
    }

    [Fact]
    public void Load_OutOfRangeSettings_AreClamped()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathIn,
            "{\"settings\":{\"focusMinutes\":0,\"shortBreakMinutes\":-3,\"longBreakMinutes\":0,\"longBreakEvery\":0,\"dailyGoalMinutes\":0,\"timerSeconds\":-5}}");

        var s = new DataStore(PathIn).Load().Settings;

        Assert.True(s.FocusMinutes >= 1);
        Assert.True(s.ShortBreakMinutes >= 1);
        Assert.True(s.LongBreakMinutes >= 1);
        Assert.True(s.LongBreakEvery >= 1);
        Assert.True(s.DailyGoalMinutes >= 5);
        Assert.True(s.TimerSeconds >= 0);
    }

    [Fact]
    public void Load_LockedFile_ReturnsDefaultsAndRefusesToOverwrite()
    {
        var store = new DataStore(PathIn);
        var data = AppData.CreateDefault();
        data.Sessions.Add(new Session { Seconds = 1500 });
        store.Save(data);
        var original = File.ReadAllText(PathIn);

        AppData loaded;
        using (new FileStream(PathIn, FileMode.Open, FileAccess.Read, FileShare.None))
            loaded = store.Load();

        Assert.Empty(loaded.Sessions);
        Assert.True(store.ReadFailed);
        Assert.False(store.Save(loaded));
        Assert.Equal(original, File.ReadAllText(PathIn));
    }

    [Fact]
    public void DeleteSubject_General_ReturnsFalse()
    {
        var data = AppData.CreateDefault();
        Assert.False(data.DeleteSubject(Subject.GeneralId));
        Assert.Single(data.Subjects);
    }

    [Fact]
    public void SubjectFor_DeletedId_ReturnsGeneral()
    {
        var data = AppData.CreateDefault();
        var math = new Subject { Id = Guid.NewGuid(), Name = "Math", Color = "#4FD1C5" };
        data.Subjects.Add(math);
        Assert.True(data.DeleteSubject(math.Id));
        Assert.Equal(Subject.GeneralId, data.SubjectFor(math.Id).Id);
    }
}
