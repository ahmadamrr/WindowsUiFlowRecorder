namespace WindowsUiFlowRecorder.Presentation;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.DependencyInjection;
using WindowsUiFlowRecorder.Infrastructure.DependencyInjection;
using WindowsUiFlowRecorder.Infrastructure.Logging;
using WindowsUiFlowRecorder.Presentation.DependencyInjection;

public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;
    public static IServiceProvider ServiceProvider =>
        _serviceProvider ?? throw new InvalidOperationException("ServiceProvider not initialized");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddApplicationLayer();
        services.AddInfrastructureLayer();
        services.AddPresentationLayer();
        services.AddLogging(builder => builder.ConfigureLocalLogging());
        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Windows UI Flow Recorder & Smart UI Scanner v0.1.0");
    }
}