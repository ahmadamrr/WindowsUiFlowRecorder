namespace WindowsUiFlowRecorder.Application.Tests;

using System.Reflection;
using FluentAssertions;

public class ArchitectureComplianceTests
{
    [Fact]
    public void Domain_HasNoUnsolicitedDependencies()
    {
        var assembly = typeof(Domain.Common.Result).Assembly;
        var refs = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n != null)
            .ToList();

        refs.Should().NotContain("FlaUI.Core");
        refs.Should().NotContain("FlaUI.UIA3");
        refs.Should().NotContain("WindowsUiFlowRecorder.Application");
        refs.Should().NotContain("WindowsUiFlowRecorder.Infrastructure");
        refs.Should().NotContain("WindowsUiFlowRecorder.Presentation");
    }

    [Fact]
    public void Application_DoesNotReferenceFlaUI()
    {
        var assembly = typeof(Application.Recording.IRecordingSessionService).Assembly;
        var refs = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        refs.Should().NotContain("FlaUI.Core");
        refs.Should().NotContain("FlaUI.UIA3");
        refs.Should().NotContain("Interop.UIAutomationClient");
    }

    [Fact]
    public void Application_DoesNotReferenceWpfNamespaces()
    {
        var assembly = typeof(Application.Recording.IRecordingSessionService).Assembly;
        var refs = assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        refs.Should().NotContain("PresentationFramework");
        refs.Should().NotContain("System.Windows");
        refs.Should().NotContain("WindowsBase");
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
    public void DependencyDirection_DomainHasNoOutgoingProjectRefs()
    {
        var domainTypes = new[]
        {
            typeof(Domain.Entities.RecordingSession),
            typeof(Domain.Policies.ActionCoalescingPolicy),
            typeof(Domain.Abstractions.ISessionRepository),
            typeof(Domain.Common.Result)
        };

        var domainAssembly = typeof(Domain.Common.Result).Assembly;
        var refs = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        refs.Should().NotContain("WindowsUiFlowRecorder.Application");
        refs.Should().NotContain("WindowsUiFlowRecorder.Infrastructure");
        refs.Should().NotContain("WindowsUiFlowRecorder.Presentation");
    }
}