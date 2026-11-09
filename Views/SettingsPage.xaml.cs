using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp.Views;

public partial class SettingsPage : UserControl
{
    private AppSettings S => App.Data.Settings;

    public SettingsPage()
    {
        InitializeComponent();

        Durations.Children.Add(NumberRow("Focus length", "min", () => S.FocusMinutes, v => S.FocusMinutes = v, 1, 180));
        Durations.Children.Add(NumberRow("Short break", "min", () => S.ShortBreakMinutes, v => S.ShortBreakMinutes = v, 1, 60, top: 10));
        Durations.Children.Add(NumberRow("Long break", "min", () => S.LongBreakMinutes, v => S.LongBreakMinutes = v, 1, 90, top: 10));
        Durations.Children.Add(NumberRow("Long break after", "blocks", () => S.LongBreakEvery, v => S.LongBreakEvery = v, 1, 12, top: 10));
        GoalPanel.Children.Add(NumberRow("Study per day", "min", () => S.DailyGoalMinutes, v => S.DailyGoalMinutes = v, 5, 1440));

        AutoBreaks.IsChecked = S.AutoStartBreaks;
        AutoFocus.IsChecked = S.AutoStartFocus;
        Sound.IsChecked = S.SoundOn;
        OnTop.IsChecked = S.Topmost;
        AtSignIn.IsChecked = SafeStartup(m => m.IsEnabled);

        // Keep the switch in sync when the title-bar pin changes it.
        void SyncOnTop() => OnTop.IsChecked = S.Topmost;
        App.DataChanged += SyncOnTop;
        Unloaded += (_, _) => App.DataChanged -= SyncOnTop;

        DataPath.Text = App.Store.FilePath;
        Version.Text = $"Stint {typeof(App).Assembly.GetName().Version?.ToString(3)}";
        BuildSubjects();
    }

    // ---------------- Numbers ----------------

    private FrameworkElement NumberRow(string label, string unit, Func<int> get, Action<int> set, int min, int max, double top = 0)
    {
        var row = new Grid { Margin = new Thickness(0, top, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        row.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });

        var box = new TextBox
        {
            Style = (Style)FindResource("FieldBox"), Width = 64, Text = get().ToString(),
            HorizontalContentAlignment = HorizontalAlignment.Center, MaxLength = 4, Padding = new Thickness(6, 5, 6, 5),
            ToolTip = $"{min} to {max}",
        };
        box.PreviewTextInput += (_, e) => e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        void Commit()
        {
            int value = int.TryParse(box.Text, out var v) ? Math.Clamp(v, min, max) : get();
            box.Text = value.ToString();
            if (value == get()) return;
            set(value);
            App.SaveData();
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        Grid.SetColumn(box, 1);
        row.Children.Add(box);

        var u = new TextBlock { Text = unit, Style = (Style)FindResource("Caption"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(u, 2);
        row.Children.Add(u);
        return row;
    }

    // ---------------- Toggles ----------------

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        // Only write the toggle that was clicked; "on top" can also change from the title-bar pin.
        bool on = ((CheckBox)sender).IsChecked == true;
        if (sender == AutoBreaks) S.AutoStartBreaks = on;
        else if (sender == AutoFocus) S.AutoStartFocus = on;
        else if (sender == Sound) S.SoundOn = on;
        else if (sender == OnTop) S.Topmost = on;
        App.SaveData();
        (Window.GetWindow(this) as MainWindow)?.ApplyTopmost();
        if (sender == Sound && S.SoundOn) Notifier.PlaySound();
    }

    private void AtSignIn_Click(object sender, RoutedEventArgs e)
    {
        bool want = AtSignIn.IsChecked == true;
        SafeStartup(m => { m.SetEnabled(want); return true; });
        AtSignIn.IsChecked = SafeStartup(m => m.IsEnabled); // show what actually happened
    }

    /// <summary>Registry access can be blocked by policy; never let that crash Settings.</summary>
    private static bool SafeStartup(Func<StartupManager, bool> action)
    {
        try { return action(StartupManager.ForCurrentApp()); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    // ---------------- Subjects ----------------

    private void BuildSubjects()
    {
        Subjects.Children.Clear();
        foreach (var subject in App.Data.Subjects)
        {
            var s = subject;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var swatch = new Button
            {
                Style = (Style)FindResource("IconButton"), ToolTip = "Change colour",
                Content = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(App.ParseColor(s.Color)) },
            };
            swatch.Click += (_, _) =>
            {
                int i = Array.IndexOf(Subject.Palette, s.Color.ToUpperInvariant());
                s.Color = Subject.Palette[(i + 1) % Subject.Palette.Length];
                App.SaveData();
                BuildSubjects();
            };
            row.Children.Add(swatch);

            var name = new TextBox
            {
                Style = (Style)FindResource("FieldBox"), Text = s.Name, MaxLength = 40, Margin = new Thickness(4, 0, 4, 0),
                Padding = new Thickness(8, 5, 8, 5), Tag = "Subject name",
            };
            name.LostFocus += (_, _) => Rename(s, name);
            name.KeyDown += (_, e) => { if (e.Key == Key.Enter) Rename(s, name); };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);

            if (s.Id != Subject.GeneralId)
            {
                var del = new Button { Style = (Style)FindResource("IconButton"), Content = "", ToolTip = "Delete subject", FontSize = 12 };
                del.Click += (_, _) =>
                {
                    App.Data.DeleteSubject(s.Id);
                    App.SaveData();
                    BuildSubjects();
                };
                Grid.SetColumn(del, 2);
                row.Children.Add(del);
            }
            else
            {
                var keep = new TextBlock { Text = "Default", Style = (Style)FindResource("Caption"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 6, 0) };
                Grid.SetColumn(keep, 2);
                row.Children.Add(keep);
            }
            Subjects.Children.Add(row);
        }
    }

    private static void Rename(Subject s, TextBox box)
    {
        var name = box.Text.Trim();
        if (name.Length == 0) { box.Text = s.Name; return; }
        if (name == s.Name) return;
        s.Name = name;
        App.SaveData();
    }

    private void AddSubject_Click(object sender, RoutedEventArgs e)
    {
        var used = App.Data.Subjects.Select(x => x.Color.ToUpperInvariant()).ToHashSet();
        var color = Subject.Palette.FirstOrDefault(c => !used.Contains(c)) ?? Subject.Palette[App.Data.Subjects.Count % Subject.Palette.Length];
        App.Data.Subjects.Add(new Subject { Name = $"Subject {App.Data.Subjects.Count + 1}", Color = color });
        App.SaveData();
        BuildSubjects();
        // Jump straight into naming the new subject.
        if (Subjects.Children[^1] is Grid g && g.Children.OfType<TextBox>().FirstOrDefault() is { } box)
        {
            box.Focus();
            box.SelectAll();
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.GetDirectoryName(App.Store.FilePath)!;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }
}
