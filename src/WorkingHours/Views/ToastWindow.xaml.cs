using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WorkingHours.Views;

/// <summary>Fallback notification window used when the tray icon is disabled.</summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _closeTimer;

    public ToastWindow(string title, string body)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _closeTimer.Tick += (_, _) => Close();
        _closeTimer.Start();

        Loaded += (_, _) =>
        {
            PositionAtCorner();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        };
        Closed += (_, _) => _closeTimer.Stop();
    }

    private void PositionAtCorner()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 8;
        Top = workArea.Bottom - ActualHeight - 8;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
