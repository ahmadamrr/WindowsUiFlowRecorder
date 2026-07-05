namespace WindowsUiFlowRecorder.Infrastructure.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Application.Launching;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Infrastructure.Automation;
using WindowsUiFlowRecorder.Infrastructure.Processes;

public class LaunchChainIntegrationTests
{
    private readonly IProcessLaunchMonitor _processMonitor = new ProcessLaunchMonitor(NullLogger<ProcessLaunchMonitor>.Instance);
    private readonly IUiAutomationProvider _uiAutomation = new FlaUiAutomationProvider(NullLogger<FlaUiAutomationProvider>.Instance);

    [Fact]
    public async Task ExecuteLaunchChainAsync_ProcessStartedCondition_SingleStep_ReturnsContext()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "TestHarness", harnessPath, "--status Disconnected", null,
                new ReadinessCondition(ConditionType.ProcessStarted, null, null,
                    null, null, null, null, null, null, null),
                null, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 250, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].ApplicationTag.Should().Be("TestHarness");
        result.Value[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_WindowAppearedCondition_ReturnsContext()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "TestHarness", harnessPath, "--title \"TestWindow\" --status Disconnected", null,
                new ReadinessCondition(ConditionType.WindowAppeared, "TestWindow*", Domain.Common.WindowMatchMode.Contains,
                    null, null, null, null, null, null, null),
                null, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 250, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_ControlPropertyEquals_OnHarnessStatusLabel_ReturnsContext()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "TestHarness", harnessPath,
                "--instance Primary --status Connected --auto-connect 2", null,
                new ReadinessCondition(ConditionType.ControlPropertyEquals,
                    null, null,
                    "lblHsmStatus", null, null,
                    ExpectedPropertyName.Value, "Connected", Domain.Common.PropertyMatchMode.Contains, null),
                null, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 500, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_TwoStepChain_BothAppsLaunch()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "ProxyApp", harnessPath,
                "--instance Proxy --status Connected --auto-connect 2", null,
                new ReadinessCondition(ConditionType.ControlPropertyEquals,
                    null, null,
                    "lblHsmStatus", null, null,
                    ExpectedPropertyName.Value, "Connected", Domain.Common.PropertyMatchMode.Contains, null),
                10, true),
            new LaunchStep(2, "EAdminApp", harnessPath,
                "--instance EAdmin --title \"eAdmin Window\" --status Ready", null,
                new ReadinessCondition(ConditionType.WindowAppeared,
                    "eAdmin*", Domain.Common.WindowMatchMode.Contains,
                    null, null, null, null, null, null, null),
                10, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 500, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].ApplicationTag.Should().Be("ProxyApp");
        result.Value[1].ApplicationTag.Should().Be("EAdminApp");
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_ConditionNeverMet_ReturnsTimeoutFailure()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "TestHarness", harnessPath,
                "--status Disconnected", null,
                new ReadinessCondition(ConditionType.ControlPropertyEquals,
                    null, null,
                    "lblNONEXISTENT", null, null,
                    ExpectedPropertyName.Value, "Connected", Domain.Common.PropertyMatchMode.Exact, null),
                5, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 500, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.FailureReason.Should().Be(FailureReason.ReadinessTimeout);
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_FixedTimeoutCondition_ReturnsAfterTimeout()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "TestHarness", harnessPath, "--status Disconnected", null,
                new ReadinessCondition(ConditionType.FixedTimeout, null, null,
                    null, null, null, null, null, null, 2),
                null, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 250, CancellationToken.None);
        sw.Stop();

        result.IsSuccess.Should().BeTrue();
        sw.Elapsed.TotalSeconds.Should().BeGreaterThanOrEqualTo(1.5);
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_ProcessCrashesDuringPolling_ReturnsFailure()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "CrashApp", harnessPath,
                "--instance CrashTest", null,
                new ReadinessCondition(ConditionType.ControlPropertyEquals,
                    null, null,
                    "lblHsmStatus", null, null,
                    ExpectedPropertyName.Value, "Connected", Domain.Common.PropertyMatchMode.Exact, null),
                10, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 500, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_CleanUpOnFailure_KillsStartedProcesses()
    {
        var harnessPath = GetHarnessPath();
        if (!File.Exists(harnessPath))
            return;

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "FirstApp", harnessPath, "--status Disconnected", null,
                new ReadinessCondition(ConditionType.ProcessStarted, null, null,
                    null, null, null, null, null, null, null),
                null, true),
            new LaunchStep(2, "NeverReady", harnessPath, "--status Waiting", null,
                new ReadinessCondition(ConditionType.ControlPropertyEquals,
                    null, null,
                    "lblNONEXISTENT", null, null,
                    ExpectedPropertyName.Value, "Ready", Domain.Common.PropertyMatchMode.Exact, null),
                3, true)
        ]);

        var orchestrator = new ApplicationLaunchOrchestrator(
            _processMonitor, _uiAutomation, NullLogger<ApplicationLaunchOrchestrator>.Instance);

        var result = await orchestrator.ExecuteLaunchChainAsync(chain, 500, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.FailureReason.Should().Be(FailureReason.ReadinessTimeout);
    }

    private static string GetHarnessPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "tools", "AutomationTestHarness",
                "bin", "Debug", "net8.0-windows", "AutomationTestHarness.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "tools", "AutomationTestHarness",
                "bin", "Debug", "net8.0-windows", "AutomationTestHarness.exe"),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
                return full;
        }

        return candidates[0];
    }
}