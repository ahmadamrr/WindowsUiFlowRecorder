namespace WindowsUiFlowRecorder.Presentation;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.DependencyInjection;
using WindowsUiFlowRecorder.Application.Profiles;
using WindowsUiFlowRecorder.Application.Recording;
using WindowsUiFlowRecorder.Application.Settings;
using WindowsUiFlowRecorder.Infrastructure.DependencyInjection;
using WindowsUiFlowRecorder.Infrastructure.Logging;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public IServiceProvider ServiceProvider => _serviceProvider!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddApplicationLayer();
        services.AddInfrastructureLayer();
        services.AddLogging(builder => builder.ConfigureLocalLogging());
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Windows UI Flow Recorder & Smart UI Scanner v0.1.0");

        var svc = _serviceProvider.GetRequiredService<IRecordingSessionService>();
        logger.LogInformation("RecordingSessionService loaded - state: {State}", svc.CurrentState);

        var profileSvc = _serviceProvider.GetRequiredService<IApplicationProfileService>();
        var profiles = profileSvc.GetAllProfilesAsync().GetAwaiter().GetResult();
        logger.LogInformation("ApplicationProfileService loaded - profiles: {Count}",
            profiles.IsSuccess ? profiles.Value?.Count ?? 0 : 0);

        var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
        var settings = settingsSvc.GetSettingsAsync().GetAwaiter().GetResult();
        if (settings.IsSuccess)
        {
            logger.LogInformation("Settings loaded - mode: {Mode}, timeout: {Timeout}s",
                settings.Value!.ScreenshotMode, settings.Value.DefaultReadinessConditionTimeoutSeconds);
        }
    }
}