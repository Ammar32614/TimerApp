using System.IO;
using System.Media;
using Forms = System.Windows.Forms;

namespace TimerApp.Services;

/// <summary>Tray icon + Windows notification (balloon tips surface as toasts on Win10/11) + end sound.</summary>
public sealed class Notifier : IDisposable
{
    private static readonly string AlarmPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Alarm01.wav");

    private readonly Forms.NotifyIcon _icon;

    /// <summary>Raised when the user double-clicks the tray icon or clicks a notification.</summary>
    public event Action? RestoreRequested;

    public Notifier()
    {
        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Stint",
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => RestoreRequested?.Invoke();
        _icon.BalloonTipClicked += (_, _) => RestoreRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Stint", null, (_, _) => RestoreRequested?.Invoke());
        menu.Items.Add("Quit", null, (_, _) => System.Windows.Application.Current.MainWindow?.Close());
        _icon.ContextMenuStrip = menu;
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } icon)
                return icon;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException) { }
        return System.Drawing.SystemIcons.Application;
    }

    public void Show(string title, string body, bool playSound)
    {
        _icon.ShowBalloonTip(5000, title, body, Forms.ToolTipIcon.None);
        if (playSound) PlaySound();
    }

    /// <summary>Plays the user's chosen alarm if there is one, otherwise the Windows alarm.</summary>
    public static void PlaySound()
    {
        var custom = System.Windows.Application.Current is TimerApp.App ? TimerApp.App.Data?.Settings.AlarmSoundPath : null;
        if (custom != null && File.Exists(custom) && PlayCustom(custom)) return;
        PlayDefault();
    }

    /// <summary>WPF's MediaPlayer handles MP3/WMA as well as WAV. Must run on the UI thread.</summary>
    private static bool PlayCustom(string path)
    {
        try
        {
            if (_media == null)
            {
                _media = new System.Windows.Media.MediaPlayer();
                _media.MediaFailed += (_, _) => PlayDefault(); // unreadable/corrupt file: still make a sound
            }
            _media.Stop();
            _media.Open(new Uri(path));
            _media.Play();
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or UriFormatException)
        {
            return false;
        }
    }

    private static System.Windows.Media.MediaPlayer? _media;

    private static void PlayDefault()
    {
        try
        {
            if (File.Exists(AlarmPath))
            {
                _player ??= new SoundPlayer(AlarmPath); // one reusable player, disposed with the notifier
                _player.Play();
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { }
        SystemSounds.Asterisk.Play();
    }

    private static SoundPlayer? _player;

    public void Dispose()
    {
        _icon.Visible = false;
        var icon = _icon.Icon;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        if (!ReferenceEquals(icon, System.Drawing.SystemIcons.Application)) icon?.Dispose();
        _player?.Dispose();
        _player = null;
        _media?.Close();
        _media = null;
    }
}
