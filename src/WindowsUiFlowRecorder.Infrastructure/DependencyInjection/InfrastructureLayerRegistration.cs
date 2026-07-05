namespace WindowsUiFlowRecorder.Infrastructure.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Infrastructure.Automation;
using WindowsUiFlowRecorder.Infrastructure.Input;
using WindowsUiFlowRecorder.Infrastructure.Persistence;
using WindowsUiFlowRecorder.Infrastructure.Processes;
using WindowsUiFlowRecorder.Infrastructure.Screenshots;

public static class InfrastructureLayerRegistration
{
    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services)
    {
        services.AddSingleton<IUiAutomationProvider, FlaUiAutomationProvider>();
        services.AddSingleton<IProcessLaunchMonitor, ProcessLaunchMonitor>();
        services.AddSingleton<IScreenshotCapturer, ScreenshotCapturer>();
        services.AddSingleton<IGlobalInputHook, GlobalInputHook>();
        services.AddSingleton<IExportWriter, ExportWriter>();
        services.AddSingleton<ISessionRepository, JsonFileSessionRepository>();
        services.AddSingleton<IApplicationProfileRepository, JsonFileProfileRepository>();
        services.AddSingleton<ISettingsRepository, JsonFileSettingsRepository>();
        return services;
    }
}