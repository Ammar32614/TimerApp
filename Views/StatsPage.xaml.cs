using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp.Views;

public partial class StatsPage : UserControl
{
    private static readonly double[] LevelOpacity = [0, 0.38, 0.58, 0.8, 1];

    public StatsPage()
    {
        InitializeComponent();
        App.DataChanged += Build;
        Unloaded += (_, _) => App.DataChanged -= Build;
        Build();
    }

    private void Build()
    {
        var data = App.Data;
        var sessions = data.Sessions;
        var today = DateOnly.FromDateTime(DateTime.Now);
        int goal = Math.Max(1, data.Settings.DailyGoalMinutes);

        // Tiles
        int todaySec = StatsCalculator.SecondsOn(sessions, today);
        TodayValue.Text = Format.Duration(todaySec);
        TodaySub.Text = todaySec >= goal * 60 ? "Goal reached" : $"{Format.Duration(goal * 60 - todaySec)} to goal";

        int streak = StatsCalculator.Streak(sessions, today);
        StreakValue.Text = streak == 1 ? "1 day" : $"{streak} days";
        StreakSub.Text = streak == 0 ? "Start one today" : todaySec > 0 ? "Including today" : "Study to extend";

        var week = StatsCalculator.Week(sessions, today, id => App.Data.SubjectFor(id).Id);
        int weekSec = week.Sum(d => d.BySubject.Values.Sum());
        int activeDays = week.Count(d => d.BySubject.Values.Sum() > 0);
        WeekValue.Text = Format.Duration(weekSec);
        WeekSub.Text = activeDays == 1 ? "on 1 day" : $"on {activeDays} days";

        BuildBars(week, goal * 60);
        BuildSubjects(week);
        BuildHeatmap(sessions, today, goal);
    }

    private void BuildBars(IReadOnlyList<(DateOnly Day, Dictionary<Guid, int> BySubject)> week, int goalSec)
    {
        BarsHost.Children.Clear();
        BarLabels.Children.Clear();
        int max = Math.Max(goalSec, week.Max(d => d.BySubject.Values.Sum()));
        const double height = 130;

        var columns = new UniformGrid { Columns = 7 };
        foreach (var (day, bySubject) in week)
        {
            int total = bySubject.Values.Sum();
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Width = 22, Background = Brushes.Transparent };
            // Largest subject at the bottom, keeps the colours stable-looking across days.
            var parts = bySubject.OrderBy(kv => kv.Value).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                var subject = App.Data.SubjectFor(parts[i].Key);
                stack.Children.Add(new Border
                {
                    Height = Math.Max(2, height * parts[i].Value / max),
                    Background = new SolidColorBrush(App.ParseColor(subject.Color)),
                    CornerRadius = i == 0 ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(0),
                    Margin = new Thickness(0, 0, 0, i == parts.Count - 1 ? 0 : 1),
                });
            }
            if (total == 0)
                stack.Children.Add(new Border { Height = 2, Background = (Brush)FindResource("Raised"), CornerRadius = new CornerRadius(1) });

            var cell = new Grid { Background = Brushes.Transparent, ToolTip = $"{day.ToString("ddd d MMM", CultureInfo.CurrentCulture)}: {Format.Duration(total)}" };
            cell.Children.Add(stack);
            columns.Children.Add(cell);

            BarLabels.Children.Add(new TextBlock
            {
                Text = day.ToString("ddd", CultureInfo.CurrentCulture)[..2],
                Style = (Style)FindResource("Caption"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)FindResource(day == DateOnly.FromDateTime(DateTime.Now) ? "Text" : "Muted"),
            });
        }
        BarsHost.Children.Add(columns);

        // Dashed goal line
        double y = height - height * goalSec / max;
        var line = new Line
        {
            X1 = 0, X2 = 1, Y1 = 0, Y2 = 0, Stretch = Stretch.Fill, StrokeThickness = 1,
            StrokeDashArray = [4, 4], VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, y, 0, 0),
            IsHitTestVisible = false,
        };
        line.SetResourceReference(Shape.StrokeProperty, "Muted");
        BarsHost.Children.Add(line);
        GoalLegend.Text = $"Dashed line: {Format.Duration(goalSec)} goal";
    }

    private void BuildSubjects(IReadOnlyList<(DateOnly Day, Dictionary<Guid, int> BySubject)> week)
    {
        SubjectRows.Children.Clear();
        var totals = week.SelectMany(d => d.BySubject)
            .GroupBy(kv => App.Data.SubjectFor(kv.Key).Id)
            .Select(g => (Subject: App.Data.SubjectFor(g.Key), Seconds: g.Sum(kv => kv.Value)))
            .OrderByDescending(t => t.Seconds)
            .ToList();

        if (totals.Count == 0)
        {
            SubjectRows.Children.Add(new TextBlock
            {
                Text = "Nothing logged in the last 7 days.", Style = (Style)FindResource("Caption"),
            });
            return;
        }

        int max = totals[0].Seconds;
        foreach (var (subject, seconds) in totals)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });

            row.Children.Add(new TextBlock { Text = subject.Name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
            var track = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            track.Children.Add(new Border { Background = (Brush)FindResource("Raised"), CornerRadius = new CornerRadius(3) });
            var fill = new Border
            {
                Background = new SolidColorBrush(App.ParseColor(subject.Color)), CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            track.Children.Add(fill);
            track.SizeChanged += (_, e) => fill.Width = e.NewSize.Width * seconds / max;
            Grid.SetColumn(track, 1);
            row.Children.Add(track);
            var t = new TextBlock { Text = Format.Duration(seconds), Style = (Style)FindResource("Caption"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(t, 2);
            row.Children.Add(t);
            SubjectRows.Children.Add(row);
        }
    }

    private void BuildHeatmap(List<Session> sessions, DateOnly today, int goalMinutes)
    {
        const int weeks = 12, cell = 18, gap = 4;
        Heat.Children.Clear();
        Heat.RowDefinitions.Clear();
        Heat.ColumnDefinitions.Clear();

        // Columns are Monday-to-Sunday weeks; the last column is the current week.
        int dow = ((int)today.DayOfWeek + 6) % 7; // Monday = 0
        var lastDay = today.AddDays(6 - dow);
        var levels = StatsCalculator.HeatLevels(sessions, lastDay, goalMinutes, weeks * 7);
        var firstDay = lastDay.AddDays(-(weeks * 7 - 1));

        Heat.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        for (int c = 0; c < weeks; c++) Heat.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cell + gap) });
        for (int r = 0; r < 7; r++) Heat.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cell + gap) });

        string[] rowLabels = ["Mon", "", "Wed", "", "Fri", "", ""];
        for (int r = 0; r < 7; r++)
        {
            var label = new TextBlock { Text = rowLabels[r], Style = (Style)FindResource("Caption"), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(label, r);
            Heat.Children.Add(label);
        }

        for (int i = 0; i < levels.Length; i++)
        {
            var day = firstDay.AddDays(i);
            var rect = new Rectangle { Width = cell, Height = cell, RadiusX = 4, RadiusY = 4, HorizontalAlignment = HorizontalAlignment.Left };
            if (day > today)
            {
                rect.Stroke = (Brush)FindResource("Hairline");
                rect.StrokeDashArray = [2, 2];
            }
            else if (levels[i] == 0)
            {
                rect.Fill = (Brush)FindResource("Raised");
            }
            else
            {
                rect.Fill = (Brush)FindResource("FocusBrush");
                rect.Opacity = LevelOpacity[levels[i]];
            }
            if (day <= today)
                rect.ToolTip = $"{day.ToString("ddd d MMM", CultureInfo.CurrentCulture)}: {Format.Duration(StatsCalculator.SecondsOn(sessions, day))}";
            Grid.SetColumn(rect, 1 + i / 7);
            Grid.SetRow(rect, i % 7);
            Heat.Children.Add(rect);
        }

        HeatLegend.Children.Clear();
        HeatLegend.Children.Add(new TextBlock { Text = "Less", Style = (Style)FindResource("Caption"), FontSize = 10, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        for (int l = 0; l <= 4; l++)
        {
            var r = new Rectangle { Width = 10, Height = 10, RadiusX = 2, RadiusY = 2, Margin = new Thickness(2, 0, 0, 0) };
            if (l == 0) r.Fill = (Brush)FindResource("Raised");
            else { r.Fill = (Brush)FindResource("FocusBrush"); r.Opacity = LevelOpacity[l]; }
            HeatLegend.Children.Add(r);
        }
        HeatLegend.Children.Add(new TextBlock { Text = "Goal met", Style = (Style)FindResource("Caption"), FontSize = 10, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
    }
}
