namespace WindowsUiFlowRecorder.Presentation.Recorder;

using System.IO;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Profiles;
using WindowsUiFlowRecorder.Application.Recording;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Presentation.Shared;

public class RecorderViewModel : ViewModelBase
{
    private readonly IRecordingSessionService _sessionService;
    private readonly IApplicationProfileService _profileService;
    private readonly ILogger<RecorderViewModel> _logger;

    private RecordingSessionState _state;
    private string _statusMessage = "Ready";
    private string _errorMessage = string.Empty;
    private string _sessionSummaryText = string.Empty;
    private bool _hasError;
    private bool _hasSessionSummary;
    private bool _canConfigure;

    public RecordingSessionState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public string SessionSummaryText
    {
        get => _sessionSummaryText;
        private set => SetProperty(ref _sessionSummaryText, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool HasSessionSummary
    {
        get => _hasSessionSummary;
        private set => SetProperty(ref _hasSessionSummary, value);
    }

    public bool CanConfigure
    {
        get => _canConfigure;
        private set => SetProperty(ref _canConfigure, value);
    }

    public AsyncRelayCommand StartRecordingCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand DismissErrorCommand { get; }

    public RecorderViewModel(
        IRecordingSessionService sessionService,
        IApplicationProfileService profileService,
        ILogger<RecorderViewModel> logger)
    {
        _sessionService = sessionService;
        _profileService = profileService;
        _logger = logger;

        State = RecordingSessionState.Idle;
        UpdateCanConfigure();

        _sessionService.StateChanged += OnSessionStateChanged;
        _sessionService.ErrorOccurred += OnError;

        StartRecordingCommand = new AsyncRelayCommand(OnStartRecordingAsync, () => CanConfigure);
        PauseCommand = new RelayCommand(OnPause, () => State == RecordingSessionState.Recording);
        ResumeCommand = new RelayCommand(OnResume, () => State == RecordingSessionState.Paused);
        StopCommand = new AsyncRelayCommand(OnStopAsync, () => State is RecordingSessionState.Recording or RecordingSessionState.Paused);
        ExportCommand = new AsyncRelayCommand(OnExportAsync, () => State is RecordingSessionState.Reviewing or RecordingSessionState.Exported);
        ResetCommand = new RelayCommand(OnReset, () => State is RecordingSessionState.Reviewing or RecordingSessionState.Exported or RecordingSessionState.LaunchFailed);
        DismissErrorCommand = new RelayCommand(_ => { HasError = false; ErrorMessage = string.Empty; });
    }

    private void OnSessionStateChanged(RecordingSessionState newState)
    {
        State = newState;
        HasSessionSummary = newState is RecordingSessionState.Reviewing or RecordingSessionState.Exported;

        StatusMessage = newState switch
        {
            RecordingSessionState.Idle => "Ready",
            RecordingSessionState.Configuring => "Configuring...",
            RecordingSessionState.LaunchingChain => "Launching target applications...",
            RecordingSessionState.Recording => "Recording active",
            RecordingSessionState.Paused => "Recording paused",
            RecordingSessionState.Stopped => "Stopping...",
            RecordingSessionState.Reviewing => "Session completed - review before export",
            RecordingSessionState.Exporting => "Exporting...",
            RecordingSessionState.Exported => "Session exported successfully",
            RecordingSessionState.LaunchFailed => "Launch failed",
            _ => "Unknown"
        };

        UpdateCanConfigure();

        if (newState == RecordingSessionState.Reviewing)
        {
            var summary = _sessionService.GetSessionSummary();
            if (summary != null)
            {
                SessionSummaryText = $"Session: {summary.Name}\n" +
                    $"Duration: {summary.DurationSeconds}s\n" +
                    $"Actions: {summary.ActionCount}\n" +
                    $"Windows: {summary.WindowCount}\n" +
                    $"Screenshots: {summary.ScreenshotCount}\n" +
                    $"Applications: {string.Join(", ", summary.TargetApplicationTags)}";
            }
        }

        RefreshCommands();
    }

    private void OnError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        _logger.LogWarning("Session error: {Message}", message);
    }

    private void UpdateCanConfigure()
    {
        CanConfigure = State == RecordingSessionState.Idle;
    }

    private void RefreshCommands()
    {
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private async Task OnStartRecordingAsync()
    {
        try
        {
            HasError = false;

            var profilesResult = await _profileService.GetAllProfilesAsync();
            var profiles = profilesResult.IsSuccess ? profilesResult.Value : [];

            if (profiles == null || profiles.Count == 0)
            {
                var defaultProfile = new ApplicationProfile(
                    Guid.NewGuid(),
                    "Default Single App",
                    "Placeholder: configure a real application path",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    new ApplicationLaunchChain([
                        new LaunchStep(1, "TargetApp", "notepad.exe", null, null,
                            new ReadinessCondition(ConditionType.ProcessStarted, null, null,
                                null, null, null, null, null, null, null),
                            null, true)
                    ]));

                var saveResult = await _profileService.SaveProfileAsync(defaultProfile);
                if (!saveResult.IsSuccess)
                {
                    HasError = true;
                    ErrorMessage = $"Failed to create default profile: {saveResult.ErrorMessage}";
                    return;
                }

                profiles = [defaultProfile];
            }

            var profile = profiles[0];

            var prepareResult = await _sessionService.PrepareAsync(profile.LaunchChain, CancellationToken.None);
            if (!prepareResult.IsSuccess)
            {
                HasError = true;
                ErrorMessage = $"Failed to prepare session: {prepareResult.ErrorMessage}";
                return;
            }

            var startResult = await _sessionService.StartRecordingAsync(CancellationToken.None);
            if (!startResult.IsSuccess)
            {
                HasError = true;
                ErrorMessage = $"Failed to start recording: {startResult.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting recording");
            HasError = true;
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
    }

    private void OnPause()
    {
        var result = _sessionService.PauseSession();
        if (!result.IsSuccess)
        {
            HasError = true;
            ErrorMessage = result.ErrorMessage ?? "Failed to pause";
        }
    }

    private void OnResume()
    {
        var result = _sessionService.ResumeSession();
        if (!result.IsSuccess)
        {
            HasError = true;
            ErrorMessage = result.ErrorMessage ?? "Failed to resume";
        }
    }

    private async Task OnStopAsync()
    {
        try
        {
            var result = await _sessionService.StopSessionAsync(CancellationToken.None);
            if (!result.IsSuccess)
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Failed to stop session";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping session");
            HasError = true;
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
    }

    private async Task OnExportAsync()
    {
        try
        {
            var exportDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"Export_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

            var result = await _sessionService.ExportSessionAsync(exportDir, CancellationToken.None);
            if (!result.IsSuccess)
            {
                HasError = true;
                ErrorMessage = $"Export failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting session");
            HasError = true;
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
    }

    private void OnReset()
    {
        _sessionService.ResetToIdle();
        HasSessionSummary = false;
        SessionSummaryText = string.Empty;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = "Ready";
    }
}