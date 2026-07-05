namespace WindowsUiFlowRecorder.Presentation;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Presentation.Recorder;
using WindowsUiFlowRecorder.Presentation.Shared;

public partial class MainWindow : Window
{
    private readonly RecorderViewModel _viewModel;
    private RecordingOverlay? _overlay;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.ServiceProvider.GetRequiredService<RecorderViewModel>();
        DataContext = _viewModel;

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecorderViewModel.State))
            {
                HandleStateChange(_viewModel.State);
            }
        };

        Loaded += (_, _) =>
        {
            _overlay = new RecordingOverlay();
            HandleStateChange(_viewModel.State);
        };
    }

    private void HandleStateChange(RecordingSessionState state)
    {
        if (_overlay == null) return;

        switch (state)
        {
            case RecordingSessionState.Recording:
                _overlay.ShowRecording();
                break;
            case RecordingSessionState.Paused:
                _overlay.ShowPaused();
                break;
            default:
                _overlay.HideOverlay();
                break;
        }
    }
}