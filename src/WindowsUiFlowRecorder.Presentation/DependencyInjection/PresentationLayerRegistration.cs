namespace WindowsUiFlowRecorder.Presentation.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using WindowsUiFlowRecorder.Presentation.Profiles;
using WindowsUiFlowRecorder.Presentation.Recorder;
using WindowsUiFlowRecorder.Presentation.Scanner;
using WindowsUiFlowRecorder.Presentation.Settings;

public static class PresentationLayerRegistration
{
    public static IServiceCollection AddPresentationLayer(this IServiceCollection services)
    {
        services.AddTransient<RecorderViewModel>();
        services.AddTransient<ScannerViewModel>();
        services.AddTransient<SessionListViewModel>();
        services.AddTransient<ProfileManagerViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}