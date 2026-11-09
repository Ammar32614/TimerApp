using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TimerApp.Views;

/// <summary>Five numbered pills for "how focused were you?". Clicking the active one clears it.</summary>
public partial class RatingPicker : UserControl
{
    private static readonly string[] Hints = ["Distracted", "Patchy", "Okay", "Focused", "Deep focus"];
    private int _value;

    public RatingPicker()
    {
        InitializeComponent();
        for (int i = 1; i <= 5; i++)
        {
            var b = new Button
            {
                Style = (Style)FindResource("ChipButton"),
                Content = i.ToString(),
                Tag = i,
                Width = 40,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = Hints[i - 1],
            };
            b.Click += (s, _) => Value = (int)((Button)s!).Tag == _value ? 0 : (int)((Button)s!).Tag;
            Row.Children.Add(b);
        }
        Paint();
    }

    public int Value
    {
        get => _value;
        set { _value = Math.Clamp(value, 0, 5); Paint(); }
    }

    private void Paint()
    {
        foreach (Button b in Row.Children)
        {
            bool on = (int)b.Tag <= _value;
            if (on) b.SetResourceReference(BackgroundProperty, "ModeAccent");
            else b.ClearValue(BackgroundProperty);
            b.Foreground = on ? (Brush)FindResource("OnAccent") : (Brush)FindResource("Muted");
        }
    }
}
