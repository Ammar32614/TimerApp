using Microsoft.Win32;
using TimerApp.Services;
using Xunit;

namespace TimerApp.Tests;

public class StartupManagerTests : IDisposable
{
    // A throwaway key so tests never touch the real Run key.
    private readonly string _key = @"Software\StintTests\" + Guid.NewGuid().ToString("N");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\StintTests", throwOnMissingSubKey: false);

    [Fact]
    public void IsEnabled_NoValue_False() =>
        Assert.False(new StartupManager(_key, @"C:\Apps\Stint.exe").IsEnabled);

    [Fact]
    public void SetEnabled_True_WritesQuotedPathWithMinimizedFlag()
    {
        var m = new StartupManager(_key, @"C:\Program Files\Stint\Stint.exe");
        m.SetEnabled(true);

        using var k = Registry.CurrentUser.OpenSubKey(_key)!;
        Assert.Equal("\"C:\\Program Files\\Stint\\Stint.exe\" --minimized", k.GetValue("Stint"));
        Assert.True(m.IsEnabled);
    }

    [Fact]
    public void SetEnabled_False_RemovesValue()
    {
        var m = new StartupManager(_key, @"C:\Apps\Stint.exe");
        m.SetEnabled(true);
        m.SetEnabled(false);
        Assert.False(m.IsEnabled);
    }

    [Fact]
    public void IsEnabled_ValuePointsElsewhere_False()
    {
        using (var k = Registry.CurrentUser.CreateSubKey(_key))
            k.SetValue("Stint", "\"D:\\Old\\Stint.exe\" --minimized");
        Assert.False(new StartupManager(_key, @"C:\Apps\Stint.exe").IsEnabled);
    }
}
