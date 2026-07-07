namespace WindowsUiFlowRecorder.Presentation.Settings;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Settings;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Presentation.Shared;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsViewModel> _logger;

    private Settings? _current;
    private ScreenshotMode _screenshotMode;
    private bool _captureElementCropped;
    private HierarchyRecaptureSensitivity _hierarchySensitivity;
    private string _defaultExportDir = string.Empty;
    private int _readinessTimeoutSeconds = 30;
    private int _readinessPollIntervalMs = 250;
    private int _maxHierarchyElements = 5000;
    private bool _verboseLogging;
    private string _statusMessage = string.Empty;
    private bool _isLoading;

    public ScreenshotMode ScreenshotMode
    {
        get => _screenshotMode;
        set => SetProperty(ref _screenshotMode, value);
    }

    public bool CaptureElementCropped
    {
        get => _captureElementCropped;
        set => SetProperty(ref _captureElementCropped, value);
    }

    public HierarchyRecaptureSensitivity HierarchySensitivity
    {
        get => _hierarchySensitivity;
        set => SetProperty(ref _hierarchySensitivity, value);
    }

    public string DefaultExportDir
    {
        get => _defaultExportDir;
        set => SetProperty(ref _defaultExportDir, value);
    }

    public int ReadinessTimeoutSeconds
    {
        get => _readinessTimeoutSeconds;
        set => SetProperty(ref _readinessTimeoutSeconds, value);
    }

    public int ReadinessPollIntervalMs
    {
        get => _readinessPollIntervalMs;
        set => SetProperty(ref _readinessPollIntervalMs, value);
    }

    public int MaxHierarchyElements
    {
        get => _maxHierarchyElements;
        set => SetProperty(ref _maxHierarchyElements, value);
    }

    public bool VerboseLogging
    {
        get => _verboseLogging;
        set => SetProperty(ref _verboseLogging, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<ScreenshotMode> ScreenshotModes { get; } =
        [ScreenshotMode.EveryAction, ScreenshotMode.WindowChangeOnly, ScreenshotMode.ManualCheckpointOnly, ScreenshotMode.Off];

    public System.Collections.ObjectModel.ObservableCollection<HierarchyRecaptureSensitivity> HierarchySensitivities { get; } =
        [HierarchyRecaptureSensitivity.Low, HierarchyRecaptureSensitivity.Medium, HierarchyRecaptureSensitivity.High];

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }
    public SyncRelayCommand BrowseExportDirCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        LoadCommand = new AsyncRelayCommand(async _ => await LoadSettingsAsync());
        SaveCommand = new AsyncRelayCommand(async _ => await SaveSettingsAsync());
        BrowseExportDirCommand = new SyncRelayCommand(OnBrowseExportDir);
    }

    public async Task LoadSettingsAsync()
    {
        IsLoading = true;
        var result = await _settingsService.GetSettingsAsync();
        if (result.IsSuccess && result.Value != null)
        {
            _current = result.Value;
            ScreenshotMode = _current.ScreenshotMode;
            CaptureElementCropped = _current.CaptureElementCroppedScreenshot;
            HierarchySensitivity = _current.HierarchyRecaptureSensitivity;
            DefaultExportDir = _current.DefaultExportDirectory ?? string.Empty;
            ReadinessTimeoutSeconds = _current.DefaultReadinessConditionTimeoutSeconds;
            ReadinessPollIntervalMs = _current.DefaultReadinessPollIntervalMilliseconds;
            MaxHierarchyElements = _current.MaxHierarchyElementCount;
            VerboseLogging = _current.VerboseDiagnosticLoggingEnabled;
            StatusMessage = "Settings loaded";
        }
        else
        {
            StatusMessage = $"Error: {result.ErrorMessage ?? "Failed to load settings"}";
        }
        IsLoading = false;
    }

    private async Task SaveSettingsAsync()
    {
        var settings = new Settings(
            ScreenshotMode, CaptureElementCropped, HierarchySensitivity,
            string.IsNullOrWhiteSpace(DefaultExportDir) ? null : DefaultExportDir,
            ReadinessTimeoutSeconds, ReadinessPollIntervalMs, MaxHierarchyElements,
            VerboseLogging, DateTime.UtcNow);

        var result = await _settingsService.UpdateSettingsAsync(settings);
        StatusMessage = result.IsSuccess ? "Settings saved" : $"Save failed: {result.ErrorMessage}";
    }

    private void OnBrowseExportDir()
       {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Default Export Directory"
        };
        if (dialog.ShowDialog() == true)
            DefaultExportDir = dialog.FolderName;
    }
}