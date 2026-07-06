namespace WindowsUiFlowRecorder.Presentation.Profiles;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Profiles;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Presentation.Shared;

public class ProfileManagerViewModel : ViewModelBase
{
    private readonly IApplicationProfileService _profileService;
    private readonly ILogger<ProfileManagerViewModel> _logger;

    private ProfileItemViewModel? _selectedProfile;
    private string _statusMessage = string.Empty;
    private bool _isLoading;
    private string _editName = string.Empty;
    private string _editDescription = string.Empty;
    private string _duplicateName = string.Empty;

    public System.Collections.ObjectModel.ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    public ProfileItemViewModel? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                EditName = value?.Name ?? string.Empty;
                EditDescription = value?.Description ?? string.Empty;
                DuplicateName = value != null ? $"{value.Name} (Copy)" : string.Empty;
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

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public string EditDescription
    {
        get => _editDescription;
        set => SetProperty(ref _editDescription, value);
    }

    public string DuplicateName
    {
        get => _duplicateName;
        set => SetProperty(ref _duplicateName, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SaveEditCommand { get; }
    public AsyncRelayCommand DuplicateCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }

    public ProfileManagerViewModel(IApplicationProfileService profileService, ILogger<ProfileManagerViewModel> logger)
    {
        _profileService = profileService;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(async _ => await LoadProfilesAsync());
        SaveEditCommand = new AsyncRelayCommand(OnSaveEditAsync, () => SelectedProfile != null);
        DuplicateCommand = new AsyncRelayCommand(OnDuplicateAsync, () => SelectedProfile != null);
        DeleteCommand = new AsyncRelayCommand(OnDeleteAsync, () => SelectedProfile != null);
    }

    public async Task LoadProfilesAsync()
    {
        IsLoading = true;
        Profiles.Clear();
        StatusMessage = "Loading profiles...";

        var result = await _profileService.GetAllProfilesAsync();
        if (result.IsSuccess && result.Value != null)
        {
            foreach (var p in result.Value.OrderBy(p => p.Name))
                Profiles.Add(new ProfileItemViewModel(p));
            StatusMessage = $"{Profiles.Count} profile(s)";
        }
        else
        {
            StatusMessage = $"Error: {result.ErrorMessage ?? "Failed to load profiles"}";
        }

        IsLoading = false;
    }

    private async Task OnSaveEditAsync()
    {
        if (SelectedProfile == null || string.IsNullOrWhiteSpace(EditName)) return;

        var existing = SelectedProfile.Source;
        var updated = existing with
        {
            Name = EditName,
            Description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription
        };

        var result = await _profileService.SaveProfileAsync(updated);
        if (result.IsSuccess)
        {
            var idx = Profiles.IndexOf(SelectedProfile);
            if (idx >= 0)
            {
                Profiles.RemoveAt(idx);
                var updatedVm = new ProfileItemViewModel(updated);
                Profiles.Insert(idx, updatedVm);
                SelectedProfile = updatedVm;
            }
            StatusMessage = "Profile updated";
        }
        else
        {
            StatusMessage = $"Save failed: {result.ErrorMessage}";
        }
    }

    private async Task OnDuplicateAsync()
    {
        if (SelectedProfile == null || string.IsNullOrWhiteSpace(DuplicateName)) return;

        var result = await _profileService.DuplicateProfileAsync(
            SelectedProfile.SessionId, DuplicateName);

        if (result.IsSuccess && result.Value != null)
        {
            Profiles.Add(new ProfileItemViewModel(result.Value));
            StatusMessage = "Profile duplicated";
        }
        else
        {
            StatusMessage = $"Duplicate failed: {result.ErrorMessage}";
        }
    }

    private async Task OnDeleteAsync()
    {
        if (SelectedProfile == null) return;

        var result = await _profileService.DeleteProfileAsync(SelectedProfile.SessionId);
        if (result.IsSuccess)
        {
            Profiles.Remove(SelectedProfile);
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage = "Profile deleted";
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

public class ProfileItemViewModel : ViewModelBase
{
    private string _name;
    private string? _description;

    public Guid SessionId { get; }
    public ApplicationProfile Source { get; }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string? Description { get => _description; set => SetProperty(ref _description, value); }
    public int StepCount => Source.LaunchChain.Steps.Count;
    public string Applications => string.Join(", ", Source.LaunchChain.Steps.Select(s => s.ApplicationTag));
    public string CreatedAtDisplay => Source.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public ProfileItemViewModel(ApplicationProfile profile)
    {
        Source = profile;
        SessionId = profile.ProfileId;
        _name = profile.Name;
        _description = profile.Description;
    }

    public string DisplayText => $"{Name}  ({StepCount} step(s): {Applications})";
}