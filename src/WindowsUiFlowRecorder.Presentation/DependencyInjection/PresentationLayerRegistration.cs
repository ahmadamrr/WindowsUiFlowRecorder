namespace WindowsUiFlowRecorder.Presentation.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;
using WindowsUiFlowRecorder.Presentation.Recorder;

public static class PresentationLayerRegistration
{
    public static IServiceCollection AddPresentationLayer(this IServiceCollection services)
    {
        services.AddTransient<RecorderViewModel>();
        return services;
    }
}