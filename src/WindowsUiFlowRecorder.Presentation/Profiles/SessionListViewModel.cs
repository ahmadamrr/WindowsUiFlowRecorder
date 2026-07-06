namespace WindowsUiFlowRecorder.Presentation.Profiles;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Presentation.Shared;

public class SessionListViewModel : ViewModelBase
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<SessionListViewModel> _logger;

    private SessionItemViewModel? _selectedSession;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private string _renameText = string.Empty;
    private string _annotateText = string.Empty;

    public System.Collections.ObjectModel.ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    public SessionItemViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                RenameText = value?.Name ?? string.Empty;
                AnnotateText = value?.Note ?? string.Empty;
                RefreshCommands();
            }
        }
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

    public string RenameText
    {
        get => _renameText;
        set => SetProperty(ref _renameText, value);
    }

    public string AnnotateText
    {
        get => _annotateText;
        set => SetProperty(ref _annotateText, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RenameCommand { get; }
    public AsyncRelayCommand AnnotateCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }

    public SessionListViewModel(ISessionRepository sessionRepository, ILogger<SessionListViewModel> logger)
    {
        _sessionRepository = sessionRepository;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(async _ => await LoadSessionsAsync());
        RenameCommand = new AsyncRelayCommand(OnRenameAsync, () => SelectedSession != null);
        AnnotateCommand = new AsyncRelayCommand(OnAnnotateAsync, () => SelectedSession != null);
        DeleteCommand = new AsyncRelayCommand(OnDeleteAsync, () => SelectedSession != null);
    }

    public async Task LoadSessionsAsync()
    {
        IsLoading = true;
        Sessions.Clear();
        StatusMessage = "Loading sessions...";

        var result = await _sessionRepository.ListSessionsAsync();
        if (result.IsSuccess && result.Value != null)
        {
            foreach (var item in result.Value.OrderByDescending(s => s.CreatedAtUtc))
                Sessions.Add(new SessionItemViewModel(item));
            StatusMessage = $"{Sessions.Count} session(s) found";
        }
        else
        {
            StatusMessage = $"Error: {result.ErrorMessage ?? "Failed to load sessions"}";
        }

        IsLoading = false;
    }

    private async Task OnRenameAsync()
    {
        if (SelectedSession == null || string.IsNullOrWhiteSpace(RenameText)) return;

        var result = await _sessionRepository.UpdateSessionMetadataAsync(
            SelectedSession.SessionId, RenameText, null);
        if (result.IsSuccess)
        {
            SelectedSession.Name = RenameText;
            StatusMessage = "Session renamed";
        }
        else
        {
            StatusMessage = $"Rename failed: {result.ErrorMessage}";
        }
    }

    private async Task OnAnnotateAsync()
    {
        if (SelectedSession == null) return;

        var result = await _sessionRepository.UpdateSessionMetadataAsync(
            SelectedSession.SessionId, null, AnnotateText);
        if (result.IsSuccess)
        {
            SelectedSession.Note = AnnotateText;
            StatusMessage = "Annotation saved";
        }
        else
        {
            StatusMessage = $"Annotation failed: {result.ErrorMessage}";
        }
    }

    private async Task OnDeleteAsync()
    {
        if (SelectedSession == null) return;

        var result = await _sessionRepository.DeleteSessionAsync(SelectedSession.SessionId);
        if (result.IsSuccess)
        {
            Sessions.Remove(SelectedSession);
            SelectedSession = Sessions.FirstOrDefault();
            StatusMessage = "Session deleted";
        }
        else
        {
            StatusMessage = $"Delete failed: {result.ErrorMessage}";
        }
    }

    private void RefreshCommands()
    {
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}

public class SessionItemViewModel : ViewModelBase
{
    private string _name;
    private string? _note;

    public Guid SessionId { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string? Note { get => _note; set => SetProperty(ref _note, value); }
    public DateTime CreatedAtUtc { get; }
    public int DurationSeconds { get; }
    public string TargetApplications { get; }
    public int ActionCount { get; }
    public int WindowCount { get; }
    public int ScreenshotCount { get; }
    public string CreatedAtDisplay => CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string DurationDisplay => $"{DurationSeconds}s";
    public string Summary => $"{ActionCount} actions, {WindowCount} windows, {ScreenshotCount} screenshots";

    public SessionItemViewModel(SessionListItem item)
    {
        SessionId = item.SessionId;
        _name = item.Name;
        _note = item.Note;
        CreatedAtUtc = item.CreatedAtUtc;
        DurationSeconds = item.DurationSeconds;
        TargetApplications = string.Join(", ", item.TargetApplicationTags);
        ActionCount = item.ActionCount;
        WindowCount = item.WindowCount;
        ScreenshotCount = item.ScreenshotCount;
    }

    public string DisplayText => $"{Name}  ({CreatedAtDisplay})  [{DurationDisplay}]  {TargetApplications}";
}