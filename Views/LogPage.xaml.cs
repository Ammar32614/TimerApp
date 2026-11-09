using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp.Views;

public partial class LogPage : UserControl
{
    private Session? _editing;          // null while adding
    private Session? _lastDeleted;
    private readonly DispatcherTimer _undoTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    public LogPage()
    {
        InitializeComponent();
        _undoTimer.Tick += (_, _) => { _undoTimer.Stop(); UndoBar.Visibility = Visibility.Collapsed; };
        App.DataChanged += Build;
        Unloaded += (_, _) => { App.DataChanged -= Build; _undoTimer.Stop(); };
        Build();
    }

    public bool CloseEditorIfOpen()
    {
        if (Editor.Visibility != Visibility.Visible) return false;
        Editor.Visibility = Visibility.Collapsed;
        return true;
    }

    // ---------------- List ----------------

    private void Build()
    {
        var sessions = App.Data.Sessions;
        List.Children.Clear();
        Empty.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ExportBtn.IsEnabled = sessions.Count > 0;

        var today = DateOnly.FromDateTime(DateTime.Now);
        int week = StatsCalculator.Week(sessions, today).Sum(d => d.BySubject.Values.Sum());
        Summary.Text = sessions.Count == 0 ? "" : $"Last 7 days: {Format.Duration(week)}";

        foreach (var day in sessions.GroupBy(s => DateOnly.FromDateTime(s.Start)).OrderByDescending(g => g.Key))
        {
            var header = new Grid { Margin = new Thickness(8, List.Children.Count == 0 ? 0 : 16, 8, 6) };
            header.Children.Add(new TextBlock { Text = DayLabel(day.Key, today), FontWeight = FontWeights.SemiBold, FontSize = 13 });
            header.Children.Add(new TextBlock
            {
                Text = Format.Duration(day.Sum(s => s.Seconds)),
                Style = (Style)FindResource("Caption"),
                HorizontalAlignment = HorizontalAlignment.Right,
            });
            List.Children.Add(header);

            foreach (var s in day.OrderByDescending(s => s.Start))
                List.Children.Add(Row(s));
        }
    }

    private static string DayLabel(DateOnly day, DateOnly today) =>
        day == today ? "Today"
        : day == today.AddDays(-1) ? "Yesterday"
        : day.Year == today.Year ? day.ToString("dddd, d MMM", CultureInfo.CurrentCulture)
        : day.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    private FrameworkElement Row(Session s)
    {
        var subject = App.Data.SubjectFor(s.SubjectId);
        var muted = (Brush)FindResource("Muted");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new Border
        {
            Width = 3, CornerRadius = new CornerRadius(1.5), Margin = new Thickness(0, 2, 10, 2),
            Background = new SolidColorBrush(App.ParseColor(subject.Color)),
        });

        var text = new StackPanel();
        var line1 = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 13 };
        line1.Inlines.Add(new System.Windows.Documents.Run(subject.Name) { FontWeight = FontWeights.SemiBold });
        if (!string.IsNullOrEmpty(s.Description))
            line1.Inlines.Add(new System.Windows.Documents.Run("  " + s.Description) { Foreground = muted });
        text.Children.Add(line1);

        var when = $"{s.Start:HH:mm}–{s.End:HH:mm}";
        if (s.Mode != SessionMode.Focus) when += s.Mode == SessionMode.Timer ? ", timer" : ", stopwatch";
        text.Children.Add(new TextBlock { Text = when, Foreground = muted, FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });
        if (!string.IsNullOrEmpty(s.Note))
            text.Children.Add(new TextBlock
            {
                Text = s.Note, Foreground = muted, FontSize = 12, FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 3, 0, 0),
            });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        // Right side: duration + rating, swapped for edit/delete on hover.
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 0, 0, 0) };
        info.Children.Add(new TextBlock
        {
            Text = Format.Duration(s.Seconds), FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Right,
        });
        if (s.Rating > 0)
        {
            var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 0), ToolTip = $"Focus {s.Rating} of 5" };
            for (int i = 1; i <= 5; i++)
            {
                var d = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, Margin = new Thickness(2, 0, 0, 0) };
                d.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, i <= s.Rating ? "FocusBrush" : "Raised");
                dots.Children.Add(d);
            }
            info.Children.Add(dots);
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Top };
        actions.Children.Add(IconButton("", "Edit", () => OpenEditor(s)));
        actions.Children.Add(IconButton("", "Delete", () => Delete(s)));
        var right = new Grid();
        right.Children.Add(info);
        right.Children.Add(actions);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        var card = new Border { Child = grid, Padding = new Thickness(10, 9, 8, 9), CornerRadius = new CornerRadius(10), Background = Brushes.Transparent };
        card.MouseEnter += (_, _) =>
        {
            card.Background = (Brush)FindResource("Surface");
            info.Visibility = Visibility.Hidden;
            actions.Visibility = Visibility.Visible;
        };
        card.MouseLeave += (_, _) =>
        {
            card.Background = Brushes.Transparent;
            info.Visibility = Visibility.Visible;
            actions.Visibility = Visibility.Collapsed;
        };
        return card;
    }

    private Button IconButton(string glyph, string tip, Action onClick)
    {
        var b = new Button { Style = (Style)FindResource("IconButton"), Content = glyph, ToolTip = tip, Width = 28, Height = 28, FontSize = 12 };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void Delete(Session s)
    {
        App.Data.Sessions.Remove(s);
        _lastDeleted = s;
        App.SaveData();
        UndoBar.Visibility = Visibility.Visible;
        _undoTimer.Stop();
        _undoTimer.Start();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_lastDeleted is { } s)
        {
            App.Data.Sessions.Add(s);
            _lastDeleted = null;
            App.SaveData();
        }
        UndoBar.Visibility = Visibility.Collapsed;
    }

    // ---------------- Export ----------------

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export study log",
            FileName = $"stint-log-{DateTime.Now:yyyy-MM-dd}.csv",
            Filter = "CSV file (*.csv)|*.csv",
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            // UTF-8 with BOM so Excel shows non-English notes correctly.
            File.WriteAllText(dlg.FileName, CsvExporter.ToCsv(App.Data.Sessions, App.Data), new System.Text.UTF8Encoding(true));
            App.Notifier.Show("Study log exported", Path.GetFileName(dlg.FileName), playSound: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            App.Notifier.Show("Couldn't export the log", ex.Message, playSound: false);
        }
    }

    // ---------------- Editor ----------------

    private void Add_Click(object sender, RoutedEventArgs e) => OpenEditor(null);

    private void OpenEditor(Session? s)
    {
        _editing = s;
        var start = s?.Start ?? DateTime.Now.AddMinutes(-25);
        EditorTitle.Text = s == null ? "Add a session" : "Edit session";
        EdSave.Content = s == null ? "Add session" : "Save changes";
        EdSubject.SelectedId = s?.SubjectId ?? App.Data.Settings.LastSubjectId;
        EdDate.Text = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        EdStart.Text = start.ToString("HH:mm", CultureInfo.InvariantCulture);
        EdMinutes.Text = s == null ? "25" : Math.Max(1, (int)Math.Round(s.Seconds / 60.0)).ToString();
        EdDesc.Text = s?.Description ?? "";
        EdNote.Text = s?.Note ?? "";
        EdRating.Value = s?.Rating ?? 0;
        EdError.Visibility = Visibility.Collapsed;
        Editor.Visibility = Visibility.Visible;
    }

    private void EdCancel_Click(object sender, RoutedEventArgs e) => Editor.Visibility = Visibility.Collapsed;

    private void EdSave_Click(object sender, RoutedEventArgs e)
    {
        string? error = null;
        if (!DateTime.TryParseExact(EdDate.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            error = "Date should look like 2026-11-10.";
        else if (!TimeOnly.TryParseExact(EdStart.Text.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            error = "Start should be a 24-hour time like 09:30.";
        else if (!int.TryParse(EdMinutes.Text.Trim(), out var minutes) || minutes < 1 || minutes > 1440)
            error = "Minutes should be a number from 1 to 1440.";
        else
        {
            var start = date.Date + time.ToTimeSpan();
            if (start > DateTime.Now) error = "That start time is in the future.";
            else
            {
                var s = _editing ?? new Session { Mode = SessionMode.Focus };
                s.Start = start;
                // Keep exact seconds when the minutes field wasn't touched.
                if (_editing == null || (int)Math.Round(_editing.Seconds / 60.0) != minutes) s.Seconds = minutes * 60;
                s.SubjectId = EdSubject.SelectedId;
                s.Description = EdDesc.Text.Trim();
                s.Note = EdNote.Text.Trim();
                s.Rating = EdRating.Value;
                if (_editing == null) App.Data.Sessions.Add(s);
                Editor.Visibility = Visibility.Collapsed;
                App.SaveData();
                return;
            }
        }
        EdError.Text = error;
        EdError.Visibility = Visibility.Visible;
    }
}
