namespace WindowsUiFlowRecorder.Presentation.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using WindowsUiFlowRecorder.Presentation.Recorder;
using WindowsUiFlowRecorder.Presentation.Scanner;

public static class PresentationLayerRegistration
{
    public static IServiceCollection AddPresentationLayer(this IServiceCollection services)
    {
        services.AddTransient<RecorderViewModel>();
        services.AddTransient<ScannerViewModel>();
        return services;
    }
}