namespace WindowsUiFlowRecorder.Presentation.Recorder;

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WindowsUiFlowRecorder.Domain.Common;

public partial class RecorderView : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(RecordingSessionState), typeof(RecorderView),
            new PropertyMetadata(RecordingSessionState.Idle));

    public RecordingSessionState State
    {
        get => (RecordingSessionState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public RecorderView()
    {
        InitializeComponent();
    }

    public static readonly IValueConverter StateToRecordingVisibility = new StateToVisibilityConverter();

    private class StateToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is RecordingSessionState.Recording ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}