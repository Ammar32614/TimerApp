using System;
using System.Diagnostics;
using System.Media;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace TimerApp;

public partial class MainWindow : Window
{
    // ---- Stopwatch state ----
    private readonly Stopwatch _sw = new();
    private readonly DispatcherTimer _swTick = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private TimeSpan _lastLap = TimeSpan.Zero;
    private int _lapCount;

    // ---- Countdown state ----
    private readonly DispatcherTimer _tmTick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private DateTime _tmEnd;
    private TimeSpan _tmRemaining;
    private bool _tmRunning;

    public MainWindow()
    {
        InitializeComponent();
        _swTick.Tick += (_, _) => SwDisplay.Text = Format(_sw.Elapsed, true);
        _tmTick.Tick += TmTick;
    }

    private static string Format(TimeSpan t, bool centis) =>
        centis
            ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}"
            : $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

    // ================= Stopwatch =================
    private void SwStart_Click(object sender, RoutedEventArgs e)
    {
        if (_sw.IsRunning)
        {
            _sw.Stop();
            _swTick.Stop();
            SwDisplay.Text = Format(_sw.Elapsed, true);
            SwStartBtn.Content = "Resume";
            SwStartBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0x57));
            SwLapBtn.IsEnabled = false;
        }
        else
        {
            _sw.Start();
            _swTick.Start();
            SwStartBtn.Content = "Pause";
            SwStartBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0x7A, 0x1E));
            SwLapBtn.IsEnabled = true;
        }
    }

    private void SwLap_Click(object sender, RoutedEventArgs e)
    {
        var total = _sw.Elapsed;
        var split = total - _lastLap;
        _lastLap = total;
        _lapCount++;
        LapList.Items.Insert(0, $"Lap {_lapCount,2}   {Format(split, true)}   |   {Format(total, true)}");
    }

    private void SwReset_Click(object sender, RoutedEventArgs e)
    {
        _sw.Reset();
        _swTick.Stop();
        _lastLap = TimeSpan.Zero;
        _lapCount = 0;
        LapList.Items.Clear();
        SwDisplay.Text = "00:00:00.00";
        SwStartBtn.Content = "Start";
        SwStartBtn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2E, 0x8B, 0x57));
        SwLapBtn.IsEnabled = false;
    }

    // ================= Countdown timer =================
    private void TmStart_Click(object sender, RoutedEventArgs e)
    {
        if (_tmRunning)
        {
            // Pause
            _tmRemaining = _tmEnd - DateTime.UtcNow;
            if (_tmRemaining < TimeSpan.Zero) _tmRemaining = TimeSpan.Zero;
            _tmTick.Stop();
            _tmRunning = false;
            TmStartBtn.Content = "Resume";
            return;
        }

        if (_tmRemaining <= TimeSpan.Zero)
        {
            _tmRemaining = ReadInput();
            if (_tmRemaining <= TimeSpan.Zero)
            {
                MessageBox.Show("Set a time greater than zero.", "Timer");
                return;
            }
        }

        _tmEnd = DateTime.UtcNow + _tmRemaining;
        _tmRunning = true;
        TmInputPanel.IsEnabled = false;
        TmStartBtn.Content = "Pause";
        TmDisplay.Text = Format(_tmRemaining, false);
        _tmTick.Start();
    }

    private void TmTick(object? sender, EventArgs e)
    {
        var left = _tmEnd - DateTime.UtcNow;
        if (left <= TimeSpan.Zero)
        {
            _tmTick.Stop();
            _tmRunning = false;
            _tmRemaining = TimeSpan.Zero;
            TmDisplay.Text = "00:00:00";
            TmStartBtn.Content = "Start";
            TmInputPanel.IsEnabled = true;
            SystemSounds.Exclamation.Play();
            MessageBox.Show("Time's up!", "Timer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        // round up so display shows 00:00:01 until it actually hits zero
        TmDisplay.Text = Format(TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds)), false);
    }

    private void TmReset_Click(object sender, RoutedEventArgs e)
    {
        _tmTick.Stop();
        _tmRunning = false;
        _tmRemaining = TimeSpan.Zero;
        TmInputPanel.IsEnabled = true;
        TmStartBtn.Content = "Start";
        TmDisplay.Text = Format(ReadInput(), false);
    }

    private TimeSpan ReadInput()
    {
        int.TryParse(TmH.Text, out var h);
        int.TryParse(TmM.Text, out var m);
        int.TryParse(TmS.Text, out var s);
        return new TimeSpan(h, m, s);
    }

    private void Digits_Only(object sender, TextCompositionEventArgs e) =>
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");

    private void Topmost_Changed(object sender, RoutedEventArgs e) =>
        Topmost = TopmostChk.IsChecked == true;
}
