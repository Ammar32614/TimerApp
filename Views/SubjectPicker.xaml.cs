using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TimerApp.Models;

namespace TimerApp.Views;

public partial class SubjectPicker : UserControl
{
    private Guid _selected = Subject.GeneralId;

    public event Action? SelectionChanged;
    /// <summary>Raised from the "Manage subjects" row; the window navigates to Settings.</summary>
    public static event Action? ManageRequested;

    public SubjectPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public Guid SelectedId
    {
        get => _selected;
        set { _selected = value; Refresh(); }
    }

    public void Refresh()
    {
        var subject = App.Data.SubjectFor(_selected);
        _selected = subject.Id;
        Dot.Fill = new SolidColorBrush(App.ParseColor(subject.Color));
        NameText.Text = subject.Name;
    }

    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        Items.Items.Clear();
        foreach (var s in App.Data.Subjects)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Ellipse
            {
                Width = 8, Height = 8, Margin = new Thickness(0, 0, 10, 0),
                Fill = new SolidColorBrush(App.ParseColor(s.Color)), VerticalAlignment = VerticalAlignment.Center,
            });
            row.Children.Add(new TextBlock { Text = s.Name, FontWeight = s.Id == _selected ? FontWeights.SemiBold : FontWeights.Normal });
            var btn = new Button { Style = (Style)FindResource("MenuRowButton"), Content = row, Tag = s.Id };
            btn.Click += (b, _) =>
            {
                _selected = (Guid)((Button)b!).Tag;
                Menu.IsOpen = false;
                Refresh();
                SelectionChanged?.Invoke();
            };
            Items.Items.Add(btn);
        }
        Menu.IsOpen = true;
    }

    private void Manage_Click(object sender, RoutedEventArgs e)
    {
        Menu.IsOpen = false;
        ManageRequested?.Invoke();
    }
}
