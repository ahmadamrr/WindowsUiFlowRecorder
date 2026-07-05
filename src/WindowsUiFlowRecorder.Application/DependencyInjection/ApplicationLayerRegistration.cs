namespace WindowsUiFlowRecorder.Application.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using WindowsUiFlowRecorder.Application.Export;
using WindowsUiFlowRecorder.Application.Launching;
using WindowsUiFlowRecorder.Application.Profiles;
using WindowsUiFlowRecorder.Application.Recording;
using WindowsUiFlowRecorder.Application.Scanning;
using WindowsUiFlowRecorder.Application.Settings;

public static class ApplicationLayerRegistration
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationLaunchOrchestrator, ApplicationLaunchOrchestrator>();
        services.AddSingleton<IUiScanService, UiScanService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IApplicationProfileService, ApplicationProfileService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddScoped<IRecordingSessionService, RecordingSessionService>();
        return services;
    }
}