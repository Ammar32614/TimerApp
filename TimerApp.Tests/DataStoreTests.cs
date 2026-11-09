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
