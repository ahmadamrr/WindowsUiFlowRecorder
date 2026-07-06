namespace WindowsUiFlowRecorder.Application.Tests;

using System.Reflection;
using FluentAssertions;

public class ArchitectureComplianceTests
{
    private static readonly string[] NetworkNamespaces =
    [
        "System.Net",
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.WebSockets",
        "Microsoft.AspNetCore",
        "Microsoft.AspNetCore.SignalR",
        "Grpc",
        "RestSharp",
        "Flurl",
        "Refit",
        "SocketIOClient"
    ];

    private static readonly string[] FlaUiNamespaces =
    [
        "FlaUI.Core",
        "FlaUI.UIA3",
        "Interop.UIAutomationClient"
    ];

    private static readonly string[] WpfNamespaces =
    [
        "PresentationFramework",
        "System.Windows",
        "WindowsBase",
        "System.Xaml"
    ];

    [Fact]
    public void Domain_HasNoUnsolicitedDependencies()
    {
        var assembly = typeof(Domain.Common.Result).Assembly;
        var refs = GetAssemblyNames(assembly);

        refs.Should().NotContain(FlaUiNamespaces);
        refs.Should().NotContain("WindowsUiFlowRecorder.Application");
        refs.Should().NotContain("WindowsUiFlowRecorder.Infrastructure");
        refs.Should().NotContain("WindowsUiFlowRecorder.Presentation");
    }

    [Fact]
    public void Domain_HasNoNetworkDependencies()
    {
        var assembly = typeof(Domain.Common.Result).Assembly;
        var refs = GetAssemblyNames(assembly);
        refs.Should().NotContain(NetworkNamespaces, "Domain must not reference any networking namespace");
    }

    [Fact]
    public void Application_DoesNotReferenceFlaUI()
    {
        var assembly = typeof(Application.Recording.IRecordingSessionService).Assembly;
        var refs = GetAssemblyNames(assembly);
        refs.Should().NotContain(FlaUiNamespaces);
    }

    [Fact]
    public void Application_DoesNotReferenceWpfNamespaces()
    {
        var assembly = typeof(Application.Recording.IRecordingSessionService).Assembly;
        var refs = GetAssemblyNames(assembly);
        refs.Should().NotContain(WpfNamespaces);
    }

    [Fact]
    public void Application_HasNoNetworkDependencies()
    {
        var assembly = typeof(Application.Recording.IRecordingSessionService).Assembly;
        var refs = GetAssemblyNames(assembly);
        refs.Should().NotContain(NetworkNamespaces, "Application must not reference any networking namespace");
    }

    [Fact]
    public void Infrastructure_ImplementsApplicationInterfaces()
    {
        var infraAssembly = typeof(Infrastructure.Automation.FlaUiAutomationProvider).Assembly;
        var appAssembly = typeof(Application.Abstractions.IUiAutomationProvider).Assembly;

        var appInterfaces = appAssembly.GetExportedTypes()
            .Where(t => t.IsInterface && t.Namespace?.Contains("Abstractions") == true)
            .ToList();

        foreach (var iface in appInterfaces)
        {
            var impl = infraAssembly.GetExportedTypes()
                .FirstOrDefault(t => t.IsClass && iface.IsAssignableFrom(t));
            impl.Should().NotBeNull($"Infrastructure should implement {iface.Name}");
        }
    }

    [Fact]
    public void Infrastructure_HasNoNetworkDependencies()
    {
        var assembly = typeof(Infrastructure.Automation.FlaUiAutomationProvider).Assembly;
        var refs = GetAssemblyNames(assembly);
        refs.Should().NotContain(NetworkNamespaces, "Infrastructure must not reference any networking namespace");
    }

    [Fact]
    public void DependencyDirection_DomainHasNoOutgoingProjectRefs()
    {
        var domainAssembly = typeof(Domain.Common.Result).Assembly;
        var refs = GetAssemblyNames(domainAssembly);

        refs.Should().NotContain("WindowsUiFlowRecorder.Application");
        refs.Should().NotContain("WindowsUiFlowRecorder.Infrastructure");
        refs.Should().NotContain("WindowsUiFlowRecorder.Presentation");
    }

    [Fact]
    public void Presentation_HasNoNetworkDependencies()
    {
        var assembly = typeof(Presentation.App).Assembly;
        var refs = GetAssemblyNames(assembly);
        refs.Should().NotContain(NetworkNamespaces, "Presentation must not reference any networking namespace");
    }

    [Fact]
    public void AllLayers_FlaUiDoesNotLeakUpward()
    {
        var domainAssembly = typeof(Domain.Common.Result).Assembly;
        var appAssembly = typeof(Application.Recording.IRecordingSessionService).Assembly;
        var presAssembly = typeof(Presentation.App).Assembly;

        var domainRefs = GetAssemblyNames(domainAssembly);
        var appRefs = GetAssemblyNames(appAssembly);
        var presRefs = GetAssemblyNames(presAssembly);

        domainRefs.Should().NotContain(FlaUiNamespaces);
        appRefs.Should().NotContain(FlaUiNamespaces);
        presRefs.Should().NotContain(FlaUiNamespaces);
    }

    [Fact]
    public void AllLayers_StrictDependencyDirection()
    {
        var domainAssembly = typeof(Domain.Common.Result).Assembly;
        var appAssembly = typeof(Application.Recording.IRecordingSessionService).Assembly;
        var infraAssembly = typeof(Infrastructure.Automation.FlaUiAutomationProvider).Assembly;
        var presAssembly = typeof(Presentation.App).Assembly;

        var domainRefs = GetAssemblyNames(domainAssembly);
        var appRefs = GetAssemblyNames(appAssembly);
        var infraRefs = GetAssemblyNames(infraAssembly);
        var presRefs = GetAssemblyNames(presAssembly);

        domainRefs.Should().NotContain("WindowsUiFlowRecorder.Application");
        domainRefs.Should().NotContain("WindowsUiFlowRecorder.Infrastructure");
        domainRefs.Should().NotContain("WindowsUiFlowRecorder.Presentation");

        appRefs.Should().NotContain("WindowsUiFlowRecorder.Infrastructure");
        appRefs.Should().NotContain("WindowsUiFlowRecorder.Presentation");

        infraRefs.Should().Contain("WindowsUiFlowRecorder.Application");
        infraRefs.Should().NotContain("WindowsUiFlowRecorder.Presentation");

        presRefs.Should().Contain("WindowsUiFlowRecorder.Application");
        presRefs.Should().Contain("WindowsUiFlowRecorder.Infrastructure");
    }

    private static List<string?> GetAssemblyNames(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();
}