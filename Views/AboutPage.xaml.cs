using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace TimerApp.Views;

public partial class AboutPage : UserControl
{
    private const string Repo = "https://github.com/Ammar32614/TimerApp";

    private static readonly (string Keys, string Action)[] ShortcutList =
    [
        ("Space", "Start / pause"),
        ("Ctrl+S", "Save the run to your log"),
        ("R", "Discard the run (with Undo)"),
        ("S", "Skip to the next focus phase"),
        ("L", "Lap (stopwatch)"),
        ("1  2  3", "Focus / Timer / Stopwatch"),
        ("Ctrl+L", "Study log"),
        ("Ctrl+T", "Stats"),
        ("Esc", "Back / close"),
    ];

    public static string AppVersion => typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppVersion}";
        // Pick the largest frame in the .ico; WPF would otherwise use a small one and blur it.
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
            new Uri("pack://application:,,,/Assets/stint.ico"),
            System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        AppIcon.Source = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        DataPath.Text = App.Store.FilePath;

        Shortcuts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        Shortcuts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < ShortcutList.Length; i++)
        {
            Shortcuts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var key = new TextBlock { Text = ShortcutList[i].Keys, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 3) };
            var what = new TextBlock { Text = ShortcutList[i].Action, FontSize = 12, Margin = new Thickness(0, 3, 0, 3) };
            what.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
            Grid.SetRow(key, i);
            Grid.SetRow(what, i);
            Grid.SetColumn(what, 1);
            Shortcuts.Children.Add(key);
            Shortcuts.Children.Add(what);
        }
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private void GitHub_Click(object sender, RoutedEventArgs e) => Open(Repo);
    private void Issue_Click(object sender, RoutedEventArgs e) => Open(Repo + "/issues/new");
    private void Releases_Click(object sender, RoutedEventArgs e) => Open(Repo + "/releases");
    private void License_Click(object sender, RoutedEventArgs e) => Open(Repo + "/blob/main/LICENSE");

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(App.Store.FilePath)!;
        Directory.CreateDirectory(dir);
        Open(dir);
    }
}
