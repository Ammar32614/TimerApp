using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TimerApp.Models;
using TimerApp.Services;

namespace TimerApp;

public partial class App : Application
{
    public static DataStore Store { get; private set; } = null!;
    public static AppData Data { get; private set; } = null!;
    public static Notifier Notifier { get; private set; } = null!;

    /// <summary>The current mode's accent brush (replaced on every mode change; prefer DynamicResource ModeAccent).</summary>
    public static SolidColorBrush Accent { get; private set; } = new((Color)ColorConverter.ConvertFromString("#F5B14C"));

    /// <summary>Raised whenever sessions, subjects or settings change, so open pages can refresh.</summary>
    public static event Action? DataChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One Stint at a time: a second launch just brings the running window forward.
        // The mutex name is also used by the installer to detect a running copy.
        _instanceMutex = new System.Threading.Mutex(true, "Stint.SingleInstance", out bool first);
        _activateSignal = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "Stint.Activate");
        if (!first)
        {
            _activateSignal.Set();
            Shutdown();
            return;
        }
        var signal = _activateSignal;
        new System.Threading.Thread(() =>
        {
            while (signal.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.BringToFront());
        }) { IsBackground = true, Name = "Stint activation listener" }.Start();

        Resources["ModeAccent"] = Accent;
        DispatcherUnhandledException += OnUnhandledException;
        // Window.Closing isn't raised on sign-out/shutdown, so keep an in-progress run here too.
        SessionEnding += (_, _) => (MainWindow as MainWindow)?.SaveBeforeExit();

        // STINT_DATA lets you point the app at a different file (demo data, testing).
        Store = new DataStore(Environment.GetEnvironmentVariable("STINT_DATA") is { Length: > 0 } p ? p : DataStore.DefaultPath);
        Data = Store.Load();
        ApplyTheme(Data.Settings.Theme);
        Notifier = new Notifier();

        MainWindow = new MainWindow();
        // Launched by "Start Stint when I sign in": stay out of the way until needed.
        if (e.Args.Contains(StartupManager.MinimizedFlag)) MainWindow.WindowState = WindowState.Minimized;
        MainWindow.Show();
    }

    private System.Threading.Mutex? _instanceMutex;
    private System.Threading.EventWaitHandle? _activateSignal;

    /// <summary>Last-resort handler: keep the log safe, write a crash file, tell the user where it is.</summary>
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string dir = System.IO.Path.GetDirectoryName(Store?.FilePath ?? DataStore.DefaultPath)!;
        string file = System.IO.Path.Combine(dir, "crash.log");
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(file, $"[{DateTime.Now:u}] {e.Exception}\n\n");
            if (Data != null) Store!.Save(Data);
        }
        catch (Exception) { /* nothing more we can do */ }
        MessageBox.Show($"Stint hit an unexpected problem and needs to close. Your study log is saved.\n\nDetails were written to:\n{file}",
            "Stint", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Notifier?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static bool _warnedReadOnly;

    public static void SaveData()
    {
        try
        {
            if (!Store.Save(Data) && !_warnedReadOnly)
            {
                _warnedReadOnly = true;
                Notifier.Show("Your study log couldn't be opened",
                    "Another program may be using data.json. New sessions won't be saved until you restart Stint.", playSound: false);
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Notifier.Show("Couldn't save your study log", ex.Message, playSound: false);
        }
        DataChanged?.Invoke();
    }

    /// <summary>
    /// Swaps in a fresh accent brush. Application resources freeze plain brushes, so each change
    /// gets a new brush whose animation starts before it is stored (animated brushes can't be frozen).
    /// </summary>
    public static void SetAccent(Color color, bool animate = true)
    {
        var from = Accent.Color;
        var brush = new SolidColorBrush(animate ? from : color);
        if (animate && SystemParameters.ClientAreaAnimation && from != color)
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(from, color, TimeSpan.FromMilliseconds(350)) { EasingFunction = new QuadraticEase() });
        else
            brush.Color = color;
        Accent = brush;
        Current.Resources["ModeAccent"] = brush;
    }

    public static Color ColorOf(string key) => (Color)Current.Resources[key];

    /// <summary>The theme currently applied.</summary>
    public static ThemeDef Theme { get; private set; } = ThemeCatalog.All[0];

    /// <summary>Raised after a theme is applied, for anything that baked colours into code.</summary>
    public static event Action? ThemeChanged;

    private static readonly Dictionary<string, string> BrushFor = new()
    {
        ["BgColor"] = "Bg", ["SurfaceColor"] = "Surface", ["RaisedColor"] = "Raised", ["TextColor"] = "Text",
        ["MutedColor"] = "Muted", ["HairlineColor"] = "Hairline", ["StrongColor"] = "Strong",
        ["ScrimColor"] = "Scrim", ["OnAccentColor"] = "OnAccent", ["FocusColor"] = "FocusBrush",
    };

    /// <summary>
    /// Swaps every theme colour/brush resource. Top-level entries override the defaults in Theme.xaml,
    /// and everything references them via DynamicResource, so open screens repaint immediately.
    /// </summary>
    public static void ApplyTheme(string? name)
    {
        Theme = ThemeCatalog.Get(name);
        var res = Current.Resources;
        foreach (var key in ThemeCatalog.Keys)
        {
            var c = ParseColor(Theme[key]);
            res[key] = c;
            if (BrushFor.TryGetValue(key, out var brushKey)) res[brushKey] = new SolidColorBrush(c);
        }
        if (Current.MainWindow != null) WindowEffects.ApplyRoundedCorners(Current.MainWindow, dark: !Theme.IsLight);
        ThemeChanged?.Invoke();
    }

    public static Color ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Colors.Gray;
        try { return ColorConverter.ConvertFromString(hex) is Color c ? c : Colors.Gray; }
        catch (Exception ex) when (ex is FormatException or NotSupportedException) { return Colors.Gray; }
    }
}
