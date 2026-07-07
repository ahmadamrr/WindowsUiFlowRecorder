namespace WindowsUiFlowRecorder.Presentation.Recorder;

using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
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
    private bool _canPause;
    private bool _canResume;
    private bool _canStop;
    private bool _canExport;
    private bool _canNewSession;
    private ApplicationProfile? _selectedProfile;
    private bool _profilesLoaded;

    public ObservableCollection<ApplicationProfile> Profiles { get; } = [];

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
        private set { SetProperty(ref _canConfigure, value); RefreshCommands(); }
    }

    public bool CanPause
    {
        get => _canPause;
        private set { SetProperty(ref _canPause, value); RefreshCommands(); }
    }

    public bool CanResume
    {
        get => _canResume;
        private set { SetProperty(ref _canResume, value); RefreshCommands(); }
    }

    public bool CanStop
    {
        get => _canStop;
        private set { SetProperty(ref _canStop, value); RefreshCommands(); }
    }

    public bool CanExport
    {
        get => _canExport;
        private set { SetProperty(ref _canExport, value); RefreshCommands(); }
    }

    public bool CanNewSession
    {
        get => _canNewSession;
        private set { SetProperty(ref _canNewSession, value); RefreshCommands(); }
    }

    public bool ProfilesLoaded
    {
        get => _profilesLoaded;
        private set => SetProperty(ref _profilesLoaded, value);
    }

    public ApplicationProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public AsyncRelayCommand StartRecordingCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand DismissErrorCommand { get; }
    public AsyncRelayCommand LoadProfilesCommand { get; }

    public RecorderViewModel(
        IRecordingSessionService sessionService,
        IApplicationProfileService profileService,
        ILogger<RecorderViewModel> logger)
    {
        _sessionService = sessionService;
        _profileService = profileService;
        _logger = logger;

        State = RecordingSessionState.Idle;
        UpdateAllStates();

        _sessionService.StateChanged += OnSessionStateChanged;
        _sessionService.ErrorOccurred += OnError;

        StartRecordingCommand = new AsyncRelayCommand(OnStartRecordingAsync, () => CanConfigure && SelectedProfile != null);
        PauseCommand = new RelayCommand(OnPause, () => CanPause);
        ResumeCommand = new RelayCommand(OnResume, () => CanResume);
        StopCommand = new AsyncRelayCommand(OnStopAsync, () => CanStop);
        ExportCommand = new AsyncRelayCommand(OnExportAsync, () => CanExport);
        ResetCommand = new RelayCommand(OnReset, () => CanNewSession);
        DismissErrorCommand = new RelayCommand(_ => { HasError = false; ErrorMessage = string.Empty; });
        LoadProfilesCommand = new AsyncRelayCommand(async _ => await LoadProfilesAsync());
    }

    public async Task LoadProfilesAsync()
    {
        try
        {
            _logger.LogInformation("Loading profiles...");
            var result = await _profileService.GetAllProfilesAsync();

            Profiles.Clear();
            SelectedProfile = null;

            if (result.IsSuccess && result.Value != null)
            {
                var list = result.Value.ToList();
                _logger.LogInformation("Got {Count} profiles from service", list.Count);

                foreach (var p in list)
                    Profiles.Add(p);

                if (Profiles.Count > 0)
                {
                    SelectedProfile = Profiles[0];
                    _logger.LogInformation("Selected profile: {Name}", Profiles[0].Name);
                }
            }
            else
            {
                _logger.LogWarning("Failed to load profiles: {Error}", result.ErrorMessage);
            }

            if (Profiles.Count == 0)
            {
                _logger.LogInformation("No profiles found, creating default Notepad profile");
                var defaultProfile = CreateDefaultProfile();
                var saveResult = await _profileService.SaveProfileAsync(defaultProfile);

                if (saveResult.IsSuccess)
                {
                    Profiles.Add(defaultProfile);
                    SelectedProfile = defaultProfile;
                    _logger.LogInformation("Default Notepad profile created and selected");
                }
                else
                {
                    HasError = true;
                    ErrorMessage = $"Failed to create default profile: {saveResult.ErrorMessage}";
                    _logger.LogError("Failed to create default profile: {Error}", saveResult.ErrorMessage);
                }
            }

            ProfilesLoaded = true;
            StatusMessage = Profiles.Count > 0
                ? $"{Profiles.Count} profile(s) loaded. Selected: {SelectedProfile?.Name ?? "(none)"}"
                : "No profiles available";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading profiles");
            HasError = true;
            ErrorMessage = $"Error loading profiles: {ex.Message}";
            ProfilesLoaded = true;
        }

        RefreshCommands();
    }

    private void OnSessionStateChanged(RecordingSessionState newState)
    {
        State = newState;
        HasSessionSummary = newState is RecordingSessionState.Reviewing or RecordingSessionState.Exported;
        UpdateAllStates();

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

    private void UpdateAllStates()
    {
        CanConfigure = State == RecordingSessionState.Idle;
        CanPause = State == RecordingSessionState.Recording;
        CanResume = State == RecordingSessionState.Paused;
        CanStop = State is RecordingSessionState.Recording or RecordingSessionState.Paused;
        CanExport = State is RecordingSessionState.Reviewing or RecordingSessionState.Exported;
        CanNewSession = State is RecordingSessionState.Reviewing or RecordingSessionState.Exported or RecordingSessionState.LaunchFailed;
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

            if (SelectedProfile == null)
            {
                HasError = true;
                ErrorMessage = "No profile selected. Load or create a profile first.";
                return;
            }

            var prepareResult = await _sessionService.PrepareAsync(SelectedProfile.LaunchChain, CancellationToken.None);
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
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Export Destination",
                DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dialog.ShowDialog() != true)
                return;

            var exportDir = dialog.FolderName;
            if (string.IsNullOrWhiteSpace(exportDir))
                return;

            var result = await _sessionService.ExportSessionAsync(exportDir, CancellationToken.None);
            if (!result.IsSuccess)
            {
                HasError = true;
                ErrorMessage = $"Export failed: {result.ErrorMessage}";
            }
            else
            {
                StatusMessage = $"Session exported to {exportDir}";
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
        UpdateAllStates();
    }

    private static ApplicationProfile CreateDefaultProfile()
    {
        return new ApplicationProfile(
            Guid.NewGuid(),
            "Notepad",
            "Launches Windows Notepad for simple UI recording tests",
            DateTime.UtcNow,
            DateTime.UtcNow,
            new ApplicationLaunchChain([
                new LaunchStep(1, "Notepad", "notepad.exe", null, null,
                    new ReadinessCondition(ConditionType.ProcessStarted, null, null,
                        null, null, null, null, null, null, null),
                    null, true)
            ]));
    }
}