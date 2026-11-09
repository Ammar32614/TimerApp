using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp.Views;

public partial class TimerPage : UserControl
{
    private static readonly int[] PresetMinutes = [5, 10, 15, 25, 45, 60];

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch _clock = new();
    private readonly FocusCycle _cycle = new();
    private readonly List<string> _laps = [];
    private SessionMode _mode = SessionMode.Focus;
    private DateTime? _runStart;
    private TimeSpan? _target;
    private TimeSpan _lastLap;
    private int _todayLoggedSeconds;
    private DateOnly _todayDate;
    private bool? _tabsLocked;
    private (int, int, Phase)? _dotsKey;
    private string _accentKey = "";
    private Session? _sheetSession;
    private bool _flashError;

    /// <summary>Raised on every visible change, so the mini view can mirror the state.</summary>
    public event Action? StateChanged;

    public TimerPage()
    {
        InitializeComponent();
        _tick.Tick += (_, _) => OnTick();

        foreach (var m in PresetMinutes)
        {
            var b = new Button
            {
                Style = (Style)FindResource("ChipButton"),
                Content = m < 60 ? $"{m} min" : "1 hour",
                Margin = new Thickness(3),
                Tag = m,
            };
            b.Click += (s, _) => { SetTimerInputs((int)((Button)s!).Tag * 60); Render(); };
            PresetsPanel.Children.Add(b);
        }

        var settings = App.Data.Settings;
        Picker.SelectedId = settings.LastSubjectId;
        Picker.SelectionChanged += () =>
        {
            App.Data.Settings.LastSubjectId = Picker.SelectedId;
            App.SaveData();
        };
        SetTimerInputs(settings.TimerSeconds);

        App.DataChanged += OnDataChanged;
        ClearRun();
        RefreshToday();
        Render();
    }

    // ---------------- Public surface (shortcuts, mini mode) ----------------

    public SessionMode Mode => _mode;
    public bool IsRunning => _clock.IsRunning;
    public bool HasActiveRun => _clock.Elapsed > TimeSpan.Zero;
    public string TimeDisplay => TimeText.Text;
    public string PhaseDisplay => PhaseText.Text;
    public double RingProgress => Ring.Progress;

    public void ToggleRun()
    {
        if (_clock.IsRunning)
        {
            _clock.Stop();
            _tick.Stop();
            Render();
            return;
        }

        if (_mode == SessionMode.Timer && !HasActiveRun)
        {
            CommitTimerInputs();
            if (App.Data.Settings.TimerSeconds <= 0)
            {
                _flashError = true;
                Render();
                return;
            }
            _target = TimeSpan.FromSeconds(App.Data.Settings.TimerSeconds);
        }

        _flashError = false;
        _runStart ??= DateTime.Now;
        _clock.Start();
        _tick.Start();
        Render();
    }

    public void ResetCurrent()
    {
        if (HasActiveRun)
        {
            var elapsed = _clock.Elapsed;
            _clock.Stop();
            _tick.Stop();
            if (IsStudyPhase && SessionRules.ShouldLog(elapsed)) Log(elapsed);
        }
        else if (_mode == SessionMode.Focus)
        {
            _cycle.Reset(); // second reset while idle starts the cycle over
        }
        _laps.Clear();
        _lastLap = TimeSpan.Zero;
        ClearRun();
        Render();
    }

    public void SkipOrLap()
    {
        switch (_mode)
        {
            case SessionMode.Stopwatch when _clock.IsRunning:
                var total = _clock.Elapsed;
                _laps.Insert(0, $"Lap {_laps.Count + 1}|{FormatStopwatch(total - _lastLap)}|{FormatStopwatch(total)}");
                _lastLap = total;
                Render();
                break;
            case SessionMode.Focus:
                var elapsed = _clock.Elapsed;
                _clock.Stop();
                _tick.Stop();
                if (IsStudyPhase && SessionRules.ShouldLog(elapsed)) Log(elapsed);
                _cycle.Advance(App.Data.Settings.LongBreakEvery);
                ClearRun();
                Render();
                break;
        }
    }

    public void SetMode(SessionMode mode)
    {
        if (mode == _mode || HasActiveRun) return;
        _mode = mode;
        (mode switch { SessionMode.Focus => FocusTab, SessionMode.Timer => TimerTab, _ => StopwatchTab }).IsChecked = true;
        _laps.Clear();
        _lastLap = TimeSpan.Zero;
        ClearRun();
        Render();
    }

    /// <summary>Called when the app closes: keep any meaningful in-progress study time.</summary>
    public void FlushOnExit()
    {
        if (_sheetSession != null) SaveSheet(skip: false);
        if (HasActiveRun && IsStudyPhase && SessionRules.ShouldLog(_clock.Elapsed))
            Log(_clock.Elapsed, showSheet: false);
    }

    public bool CloseSheetIfOpen()
    {
        if (_sheetSession == null) return false;
        SaveSheet(skip: true);
        return true;
    }

    // ---------------- Core ----------------

    private bool IsStudyPhase => _mode != SessionMode.Focus || _cycle.Current == Phase.Focus;

    private void ClearRun()
    {
        _clock.Reset();
        _runStart = null;
        _target = _mode switch
        {
            SessionMode.Focus => FocusCycle.DurationOf(_cycle.Current, App.Data.Settings),
            SessionMode.Timer => TimeSpan.FromSeconds(App.Data.Settings.TimerSeconds),
            _ => null,
        };
    }

    private void OnTick()
    {
        if (_target is { } t && _clock.Elapsed >= t) Complete(t);
        else Render();
    }

    private void Complete(TimeSpan duration)
    {
        _clock.Stop();
        _tick.Stop();
        var settings = App.Data.Settings;
        bool study = IsStudyPhase;
        var subject = App.Data.SubjectFor(Picker.SelectedId).Name;

        if (study) Log(duration);

        if (_mode == SessionMode.Focus)
        {
            var finished = _cycle.Current;
            _cycle.Advance(settings.LongBreakEvery);
            var next = _cycle.Current;
            ClearRun();
            App.Notifier.Show(
                finished == Phase.Focus ? "Focus block done" : "Break's over",
                finished == Phase.Focus
                    ? $"{Minutes(duration)} of {subject} logged. Time for a {(next == Phase.LongBreak ? "long" : "short")} break."
                    : "Ready for the next focus block?",
                settings.SoundOn);
            bool auto = next == Phase.Focus ? settings.AutoStartFocus : settings.AutoStartBreaks;
            if (auto) ToggleRun();
        }
        else
        {
            ClearRun();
            App.Notifier.Show("Time's up", $"{Minutes(duration)} of {subject} logged.", settings.SoundOn);
        }
        Render(); // no Activate(): the toast is enough, don't steal focus from what the user is writing
    }

    private void Log(TimeSpan elapsed, bool showSheet = true)
    {
        var session = new Session
        {
            Start = _runStart ?? DateTime.Now - elapsed,
            Seconds = (int)Math.Round(elapsed.TotalSeconds),
            Mode = _mode,
            SubjectId = Picker.SelectedId,
            Description = DescBox.Text.Trim(),
        };
        App.Data.Sessions.Add(session);
        App.SaveData();
        if (showSheet) ShowSheet(session);
    }

    // ---------------- Rendering ----------------

    private void Render()
    {
        var settings = App.Data.Settings;
        var elapsed = _clock.Elapsed;
        bool active = HasActiveRun;

        // Time + ring
        if (_target is { } target)
        {
            var left = target - elapsed;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            TimeText.Text = FormatCountdown(TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds)));
            SetProgress(target.TotalSeconds > 0 ? left.TotalSeconds / target.TotalSeconds : 0);
        }
        else
        {
            TimeText.Text = FormatStopwatch(elapsed);
            SetProgress(active ? elapsed.TotalSeconds % 60 / 60 : 0);
        }

        bool editingTimer = _mode == SessionMode.Timer && !active;
        TimerEdit.Visibility = editingTimer ? Visibility.Visible : Visibility.Collapsed;
        TimeText.Visibility = editingTimer ? Visibility.Collapsed : Visibility.Visible;

        // Phase caption
        string phase = _mode switch
        {
            SessionMode.Focus => _cycle.Current switch
            {
                Phase.Focus => "Focus",
                Phase.ShortBreak => "Short break",
                _ => "Long break",
            },
            SessionMode.Timer => editingTimer ? "Set a time" : "Timer",
            _ => active ? "Stopwatch" : "Ready",
        };
        if (active && !_clock.IsRunning) phase = "Paused";
        PhaseText.Text = _flashError ? "Set a time above zero first" : phase;
        PhaseText.Foreground = (Brush)FindResource(_flashError ? "Danger" : "Muted");

        // Under-ring area
        DotsPanel.Visibility = _mode == SessionMode.Focus ? Visibility.Visible : Visibility.Collapsed;
        PresetsPanel.Visibility = editingTimer ? Visibility.Visible : Visibility.Collapsed;
        LapsScroll.Visibility = _mode == SessionMode.Stopwatch ? Visibility.Visible : Visibility.Collapsed;
        if (_mode == SessionMode.Focus) RenderDots(settings.LongBreakEvery);
        if (_mode == SessionMode.Stopwatch) RenderLaps();

        // Controls
        PlayBtn.Content = _clock.IsRunning ? "" : "";
        PlayBtn.ToolTip = _clock.IsRunning ? "Pause (Space)" : active ? "Resume (Space)" : "Start (Space)";
        switch (_mode)
        {
            case SessionMode.Focus:
                SideBtn.Visibility = Visibility.Visible;
                SideBtn.Content = "";
                SideBtn.ToolTip = _cycle.Current == Phase.Focus ? "Skip to break (S)" : "Skip break (S)";
                SideBtn.IsEnabled = true;
                break;
            case SessionMode.Stopwatch:
                SideBtn.Visibility = Visibility.Visible;
                SideBtn.Content = "";
                SideBtn.ToolTip = "Lap (L)";
                SideBtn.IsEnabled = _clock.IsRunning;
                break;
            default:
                SideBtn.Visibility = Visibility.Hidden;
                break;
        }
        ResetBtn.ToolTip = active ? "Reset (R)" : _mode == SessionMode.Focus ? "Restart cycle (R)" : "Reset (R)";

        // Mode switching is locked while a run is in progress, so time is never lost by accident.
        if (active != _tabsLocked)
        {
            _tabsLocked = active;
            foreach (var tab in new[] { FocusTab, TimerTab, StopwatchTab })
            {
                tab.IsEnabled = !active || tab.IsChecked == true;
                tab.ToolTip = active && tab.IsChecked != true
                    ? "Reset the current run to switch"
                    : tab == FocusTab ? "Focus blocks with breaks (1)" : tab == TimerTab ? "Plain countdown (2)" : "Count up with laps (3)";
            }
            Picker.IsEnabled = !active;
        }

        // Accent follows mode/phase
        SetAccentKey(_mode switch
        {
            SessionMode.Focus => _cycle.Current == Phase.Focus ? "FocusColor" : "BreakColor",
            SessionMode.Timer => "TimerColor",
            _ => "StopwatchColor",
        });

        // Today strip includes the run in progress (attributed, like the log, to the day it started)
        if (DateOnly.FromDateTime(DateTime.Now) != _todayDate) RefreshToday();
        bool runStartedToday = _runStart is null || DateOnly.FromDateTime(_runStart.Value) == _todayDate;
        int live = active && IsStudyPhase && runStartedToday ? (int)elapsed.TotalSeconds : 0;
        int today = _todayLoggedSeconds + live;
        TodayRun.Text = FormatDuration(today);
        GoalRun.Text = $" of {FormatDuration(settings.DailyGoalMinutes * 60)}";
        GoalBar.Width = 180 * Math.Min(1, today / (Math.Max(1, settings.DailyGoalMinutes) * 60.0));

        StateChanged?.Invoke();
    }

    private void SetProgress(double p)
    {
        Ring.Progress = p;
        Glow.Progress = p;
    }

    private void SetAccentKey(string key)
    {
        if (key == _accentKey) return;
        App.SetAccent(App.ColorOf(key), animate: _accentKey != "");
        _accentKey = key;
    }

    private void RenderDots(int every)
    {
        every = Math.Clamp(every, 1, 12);
        int done = _cycle.Current == Phase.LongBreak ? every : _cycle.CompletedFocus % every;
        var key = (every, done, _cycle.Current);
        if (key == _dotsKey) return; // only rebuild when the cycle actually moved
        _dotsKey = key;
        DotsPanel.Children.Clear();
        for (int i = 0; i < every; i++)
        {
            bool filled = i < done;
            bool current = i == done && _cycle.Current == Phase.Focus;
            var dot = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(4, 0, 4, 0) };
            if (filled) dot.SetResourceReference(Shape.FillProperty, "FocusBrush");
            else if (current) { dot.SetResourceReference(Shape.StrokeProperty, "FocusBrush"); dot.StrokeThickness = 1.5; }
            else dot.SetResourceReference(Shape.FillProperty, "Raised");
            DotsPanel.Children.Add(dot);
        }
    }

    private void RenderLaps()
    {
        if (LapsList.Items.Count == _laps.Count) return;
        LapsList.Items.Clear();
        for (int i = 0; i < _laps.Count; i++)
        {
            var parts = _laps[i].Split('|');
            var row = new Grid { Margin = new Thickness(4, 2, 4, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var brush = (Brush)FindResource(i == 0 ? "Text" : "Muted");
            row.Children.Add(Cell(parts[0], 0, brush));
            row.Children.Add(Cell(parts[1], 1, brush));
            row.Children.Add(Cell(parts[2], 2, (Brush)FindResource("Muted")));
            LapsList.Items.Add(row);
        }

        static TextBlock Cell(string text, int col, Brush fg)
        {
            var t = new TextBlock
            {
                Text = text, Foreground = fg, FontSize = 13,
                HorizontalAlignment = col == 2 ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            };
            Typography.SetNumeralAlignment(t, FontNumeralAlignment.Tabular);
            Grid.SetColumn(t, col);
            return t;
        }
    }

    private void RefreshToday()
    {
        var data = App.Data;
        _todayDate = DateOnly.FromDateTime(DateTime.Now);
        _todayLoggedSeconds = StatsCalculator.SecondsOn(data.Sessions, _todayDate);
        int streak = StatsCalculator.Streak(data.Sessions, _todayDate);
        StreakText.Text = streak switch { 0 => "No streak yet", 1 => "1-day streak", _ => $"{streak}-day streak" };
    }

    private void OnDataChanged()
    {
        RefreshToday();
        if (!HasActiveRun) ClearRun(); // pick up changed durations
        Picker.Refresh();               // e.g. the selected subject was renamed or deleted
        Render();
    }

    // ---------------- Timer inputs ----------------

    private void SetTimerInputs(int totalSeconds)
    {
        totalSeconds = Math.Clamp(totalSeconds, 0, 99 * 3600 + 59 * 60 + 59);
        TmH.Text = (totalSeconds / 3600).ToString();
        TmM.Text = (totalSeconds / 60 % 60).ToString("00");
        TmS.Text = (totalSeconds % 60).ToString("00");
        CommitTimerInputs();
    }

    private void CommitTimerInputs()
    {
        int.TryParse(TmH.Text, out var h);
        int.TryParse(TmM.Text, out var m);
        int.TryParse(TmS.Text, out var s);
        int total = h * 3600 + Math.Min(m, 59) * 60 + Math.Min(s, 59);
        if (total != App.Data.Settings.TimerSeconds)
        {
            App.Data.Settings.TimerSeconds = total;
            App.SaveData();
        }
        if (total > 0) _flashError = false;
    }

    private void TimerInput_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitTimerInputs();
        SetTimerInputs(App.Data.Settings.TimerSeconds); // normalise "7" -> "07", 75 -> 59
        if (!HasActiveRun) ClearRun();
        Render();
    }

    private void Digits_Only(object sender, TextCompositionEventArgs e) =>
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");

    private void Select_All(object sender, KeyboardFocusChangedEventArgs e) => ((TextBox)sender).SelectAll();

    // ---------------- Session sheet ----------------

    private void ShowSheet(Session s)
    {
        if (_sheetSession != null) SaveSheet(skip: false); // keep a half-written note for the previous session
        _sheetSession = s;
        var subject = App.Data.SubjectFor(s.SubjectId).Name;
        SheetTitle.Text = $"{Minutes(TimeSpan.FromSeconds(s.Seconds))} of {subject} logged";
        SheetSub.Text = string.IsNullOrEmpty(s.Description) ? "Nice work. Add a quick note if it helps." : s.Description;
        SheetRating.Value = 0;
        SheetNote.Text = "";
        Sheet.Visibility = Visibility.Visible;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        SheetShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        Sheet.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    private void SaveSheet(bool skip)
    {
        if (_sheetSession is { } s && !skip)
        {
            s.Rating = SheetRating.Value;
            s.Note = SheetNote.Text.Trim();
            App.SaveData();
        }
        _sheetSession = null;
        Sheet.Visibility = Visibility.Collapsed;
    }

    private void SheetSave_Click(object sender, RoutedEventArgs e) => SaveSheet(skip: false);
    private void SheetSkip_Click(object sender, RoutedEventArgs e) => SaveSheet(skip: true);
    private void SheetBackdrop_MouseDown(object sender, MouseButtonEventArgs e) => SaveSheet(skip: true);

    // ---------------- Handlers ----------------

    private void Play_Click(object sender, RoutedEventArgs e) => ToggleRun();
    private void Reset_Click(object sender, RoutedEventArgs e) => ResetCurrent();
    private void Side_Click(object sender, RoutedEventArgs e) => SkipOrLap();

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        var mode = sender == TimerTab ? SessionMode.Timer : sender == StopwatchTab ? SessionMode.Stopwatch : SessionMode.Focus;
        SetMode(mode);
    }

    // ---------------- Formatting ----------------

    public static string FormatCountdown(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";

    public static string FormatStopwatch(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}";

    public static string FormatDuration(int seconds)
    {
        int m = seconds / 60;
        return m < 60 ? $"{m}m" : m % 60 == 0 ? $"{m / 60}h" : $"{m / 60}h {m % 60}m";
    }

    private static string Minutes(TimeSpan t)
    {
        int m = (int)Math.Round(t.TotalMinutes);
        return m >= 60 ? FormatDuration(m * 60) : $"{Math.Max(1, m)} min";
    }
}
