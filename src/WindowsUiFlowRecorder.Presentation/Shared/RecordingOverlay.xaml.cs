namespace WindowsUiFlowRecorder.Presentation.Shared;

using System.Windows;
using System.Windows.Threading;
using WindowsUiFlowRecorder.Domain.Common;

public partial class RecordingOverlay : Window
{
    private readonly DispatcherTimer _blinkTimer;
    private bool _dotVisible;

    public RecordingOverlay()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        Left = SystemParameters.WorkArea.Right - Width - 10;
        Top = SystemParameters.WorkArea.Top + 10;

        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        _blinkTimer.Tick += (_, _) =>
        {
            _dotVisible = !_dotVisible;
            RecordingDot.Visibility = _dotVisible ? Visibility.Visible : Visibility.Hidden;
        };
    }

    public void ShowRecording()
    {
        StatusLabel.Text = "Recording";
        RecordingDot.Fill = System.Windows.Media.Brushes.Red;
        _dotVisible = true;
        RecordingDot.Visibility = Visibility.Visible;
        _blinkTimer.Start();
        Show();
    }

    public void ShowPaused()
    {
        StatusLabel.Text = "Paused";
        RecordingDot.Fill = System.Windows.Media.Brushes.Orange;
        _dotVisible = true;
        RecordingDot.Visibility = Visibility.Visible;
        _blinkTimer.Stop();
    }

    public void HideOverlay()
    {
        _blinkTimer.Stop();
        Hide();
    }
}