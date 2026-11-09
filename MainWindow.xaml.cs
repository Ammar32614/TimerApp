using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shell;
using TimerApp.Models;
using TimerApp.Services;
using TimerApp.Views;

namespace TimerApp;

public partial class MainWindow : Window
{
    private const double FullWidth = 380, FullHeight = 640, MiniWidth = 280, MiniHeight = 72;

    private readonly TimerPage _timer = new();
    private bool _mini;

    public MainWindow()
    {
        InitializeComponent();
        HomeHost.Content = _timer;
        _timer.StateChanged += UpdateMini;

        SubjectPicker.ManageRequested += () => Navigate(new SettingsPage(), "Settings");
        App.Notifier.RestoreRequested += RestoreFromTray;

        ApplyTopmost();
        RestorePosition();
        SourceInitialized += (_, _) => WindowEffects.ApplyRoundedCorners(this, dark: !App.Theme.IsLight);
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
        Activated += (_, _) => Notifier.StopSound(); // coming back to Stint silences the alarm
    }

    /// <summary>Brings the window forward, e.g. when Stint is launched a second time.</summary>
    public void BringToFront() => RestoreFromTray();

    // ---------------- Navigation ----------------

    /// <summary>Pages opened from inside another page (About from Settings); Back returns to them.</summary>
    private readonly Stack<(UserControl Page, string Title)> _parents = new();

    /// <summary>Opens a page. <paramref name="child"/> = opened from the current page, so Back returns there.</summary>
    public void Navigate(UserControl page, string title, bool child = false)
    {
        if (_mini) ToggleMini();
        if (child && PageHost.Content is UserControl current) _parents.Push((current, PageTitleText.Text));
        else if (!child) _parents.Clear();
        PageHost.Content = page;
        PageHost.Visibility = Visibility.Visible;
        PageTitleText.Text = title;
        Brand.Visibility = Visibility.Collapsed;
        BackBar.Visibility = Visibility.Visible;
        LogBtn.Visibility = page is LogPage ? Visibility.Collapsed : Visibility.Visible;
        StatsBtn.Visibility = page is StatsPage ? Visibility.Collapsed : Visibility.Visible;
        SettingsBtn.Visibility = page is SettingsPage ? Visibility.Collapsed : Visibility.Visible;
    }

    public void GoHome()
    {
        _parents.Clear();
        PageHost.Content = null;
        PageHost.Visibility = Visibility.Collapsed;
        Brand.Visibility = Visibility.Visible;
        BackBar.Visibility = Visibility.Collapsed;
        LogBtn.Visibility = StatsBtn.Visibility = SettingsBtn.Visibility = Visibility.Visible;
    }

    private bool OnSubPage => PageHost.Visibility == Visibility.Visible;

    private void Back_Click(object sender, RoutedEventArgs e) => GoBack();

    private void GoBack()
    {
        if (PageHost.Content is LogPage log && log.CloseEditorIfOpen()) return;
        if (_parents.Count > 0)
        {
            var (page, title) = _parents.Pop();
            // Rebuild rather than re-show: pages unhook their live-update events when unloaded.
            Navigate((UserControl)Activator.CreateInstance(page.GetType())!, title, child: true);
            _parents.Pop(); // Navigate(child) pushed the page we're leaving; drop it
            return;
        }
        GoHome();
    }

    private void Log_Click(object sender, RoutedEventArgs e) => Navigate(new LogPage(), "Study log");
    private void Stats_Click(object sender, RoutedEventArgs e) => Navigate(new StatsPage(), "Stats");
    private void Settings_Click(object sender, RoutedEventArgs e) => Navigate(new SettingsPage(), "Settings");

    // ---------------- Window controls ----------------

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        App.Data.Settings.Topmost = !App.Data.Settings.Topmost;
        App.SaveData();
        ApplyTopmost();
    }

    public void ApplyTopmost()
    {
        Topmost = App.Data.Settings.Topmost;
        PinBtn.Content = Topmost ? "" : "";
        PinBtn.ToolTip = Topmost ? "Stop keeping on top" : "Keep on top";
        PinBtn.SetResourceReference(ForegroundProperty, Topmost ? "ModeAccent" : "Muted");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Mini_Click(object sender, RoutedEventArgs e) => ToggleMini();
    private void MiniPlay_Click(object sender, RoutedEventArgs e) => _timer.ToggleRun();

    private void ToggleMini()
    {
        _mini = !_mini;
        if (_mini) GoHome(); // mini mirrors the timer; shortcuts must reach it
        FullView.Visibility = _mini ? Visibility.Collapsed : Visibility.Visible;
        MiniView.Visibility = _mini ? Visibility.Visible : Visibility.Collapsed;
        double right = Left + Width;
        Width = _mini ? MiniWidth : FullWidth;
        Height = _mini ? MiniHeight : FullHeight;
        Left = right - Width; // grow/shrink from the right edge, where mini windows usually live
        WindowChrome.GetWindowChrome(this).CaptionHeight = _mini ? MiniHeight : 40;
        ClampToScreen();
        UpdateMini();
    }

    private void UpdateMini()
    {
        if (!_mini) return;
        MiniTime.Text = _timer.TimeDisplay;
        MiniPhase.Text = _timer.Mode == SessionMode.Timer && !_timer.HasActiveRun ? "Timer" : _timer.PhaseDisplay;
        MiniRing.Progress = _timer.RingProgress;
        MiniPlay.Content = _timer.IsRunning ? "" : "";
    }

    private void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    // ---------------- Keyboard ----------------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool typing = Keyboard.FocusedElement is TextBox;
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (e.Key == Key.Escape)
        {
            if (typing) Keyboard.ClearFocus();
            else if (_mini) ToggleMini(); // first leave mini, which also reveals any open sheet
            else if (_timer.CloseSheetIfOpen()) { }
            else if (OnSubPage) GoBack();
            e.Handled = true;
            return;
        }
        if (ctrl && e.Key == Key.L) { Navigate(new LogPage(), "Study log"); e.Handled = true; return; }
        if (ctrl && e.Key == Key.T) { Navigate(new StatsPage(), "Stats"); e.Handled = true; return; }
        if (ctrl && e.Key == Key.S && !OnSubPage) { _timer.SaveNow(); e.Handled = true; return; }
        if (typing || ctrl || OnSubPage) return;
        // Mini mode can't show the Undo bar, so don't let a stray R/S throw a run away there.
        if (_mini && e.Key is Key.R or Key.S) { e.Handled = true; return; }

        switch (e.Key)
        {
            case Key.Space: _timer.ToggleRun(); break;
            case Key.R: _timer.ResetCurrent(); break;
            case Key.L when _timer.Mode == SessionMode.Stopwatch: _timer.SkipOrLap(); break;
            case Key.S when _timer.Mode == SessionMode.Focus: _timer.SkipOrLap(); break;
            case Key.D1 or Key.NumPad1: _timer.SetMode(SessionMode.Focus); break;
            case Key.D2 or Key.NumPad2: _timer.SetMode(SessionMode.Timer); break;
            case Key.D3 or Key.NumPad3: _timer.SetMode(SessionMode.Stopwatch); break;
            default: return;
        }
        e.Handled = true;
    }

    // ---------------- Position ----------------

    private void RestorePosition()
    {
        var s = App.Data.Settings;
        if (s.WindowLeft is { } l && s.WindowTop is { } t && IsOnScreen(l, t))
        {
            Left = l;
            Top = t;
        }
        else
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
        }
    }

    private bool IsOnScreen(double left, double top)
    {
        // The title bar's middle must sit on an actual monitor (handles unplugged monitors and
        // gaps between monitors of different sizes). Screen works in pixels, WPF in DIPs.
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var px = new System.Drawing.Point(
            (int)((left + Width / 2) * dpi.DpiScaleX),
            (int)((top + 20) * dpi.DpiScaleY));
        return System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.Contains(px));
    }

    private void ClampToScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
    }

    private void OnClosing(object? sender, CancelEventArgs e) => SaveBeforeExit();

    private bool _savedForExit;

    /// <summary>Logs any in-progress run and saves position. Safe to call from both Closing and SessionEnding.</summary>
    public void SaveBeforeExit()
    {
        if (_savedForExit) return;
        _savedForExit = true;
        _timer.FlushOnExit();
        var s = App.Data.Settings;
        if (WindowState == WindowState.Normal)
        {
            // Store the full-size position even when closing from mini mode.
            s.WindowLeft = _mini ? Left + Width - FullWidth : Left;
            s.WindowTop = Top;
        }
        App.SaveData();
    }
}
