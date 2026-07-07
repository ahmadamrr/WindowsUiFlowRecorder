namespace WindowsUiFlowRecorder.Presentation;

using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Presentation.Recorder;
using WindowsUiFlowRecorder.Presentation.Scanner;
using WindowsUiFlowRecorder.Presentation.Shared;

public partial class MainWindow : Window
{
    private readonly RecorderViewModel _recorderViewModel;
    private readonly ScannerViewModel _scannerViewModel;
    private RecordingOverlay? _overlay;
    private ElementHighlightWindow? _highlight;

    public MainWindow()
    {
        InitializeComponent();

        _recorderViewModel = App.ServiceProvider.GetRequiredService<RecorderViewModel>();
        _scannerViewModel = App.ServiceProvider.GetRequiredService<ScannerViewModel>();

        _recorderViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecorderViewModel.State))
                HandleStateChange(_recorderViewModel.State);
        };

        _scannerViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScannerViewModel.HighlightBounds))
                HandleHighlightChange(_scannerViewModel.HighlightBounds);
        };

        Loaded += (_, _) =>
        {
            _overlay = new RecordingOverlay();
            _highlight = new ElementHighlightWindow();
            HandleStateChange(_recorderViewModel.State);
            _ = _recorderViewModel.LoadProfilesAsync();
        };
    }

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source is TabControl tabControl)
        {
            var selectedItem = tabControl.SelectedItem as TabItem;
            if (selectedItem?.Header?.ToString() == "Flow Recorder")
            {
                _ = _recorderViewModel.LoadProfilesAsync();
            }
        }
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

    private void HandleHighlightChange(BoundingRectangle? bounds)
    {
        if (_highlight == null) return;

        if (bounds.HasValue)
            _highlight.ShowHighlight(bounds.Value);
        else
            _highlight.HideHighlight();
    }
}