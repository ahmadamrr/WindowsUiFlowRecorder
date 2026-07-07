namespace WindowsUiFlowRecorder.Application.Tests;

using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Application.Export;
using WindowsUiFlowRecorder.Application.Launching;
using WindowsUiFlowRecorder.Application.Recording;
using WindowsUiFlowRecorder.Application.Settings;
using WindowsUiFlowRecorder.Domain.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Domain.Policies;

public class EndToEndFlowTests
{
    [Fact]
    public async Task FullRecordingFlow_SelectProfile_Record_Stop_Export_ProducesValidPackage()
    {
        var launchMock = new Mock<IApplicationLaunchOrchestrator>();
        var inputMock = new Mock<IGlobalInputHook>();
        var uiaMock = new Mock<IUiAutomationProvider>();
        var screenshotMock = new Mock<IScreenshotCapturer>();
        var processMock = new Mock<IProcessLaunchMonitor>();
        var exportMock = new Mock<IExportService>();
        var repoMock = new Mock<ISessionRepository>();
        var settingsMock = new Mock<ISettingsService>();

        settingsMock.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(Result<Settings>.Success(new Settings(
                ScreenshotMode.EveryAction, false, HierarchyRecaptureSensitivity.Medium,
                null, 30, 250, 5000, false, DateTime.UtcNow)));

        var service = new RecordingSessionService(
            launchMock.Object, inputMock.Object, uiaMock.Object,
            screenshotMock.Object, processMock.Object, exportMock.Object,
            repoMock.Object, settingsMock.Object,
            Mock.Of<ILogger<RecordingSessionService>>());

        // Build a launch chain like a real profile would have
        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "Calculator", "calc.exe", null, null,
                new ReadinessCondition(ConditionType.ProcessStarted, null, null,
                    null, null, null, null, null, null, null),
                null, true)
        ]);

        // Step 1: Prepare (Idle → Configuring)
        var prepareResult = await service.PrepareAsync(chain, CancellationToken.None);
        prepareResult.IsSuccess.Should().BeTrue();
        service.CurrentState.Should().Be(RecordingSessionState.Configuring);

        // Step 2: Start recording (Configuring → LaunchingChain → Recording)
        var context = new TargetApplicationContext(
            "Calculator", "calc.exe", 1001, 1, DateTime.UtcNow,
            true, null, TargetTerminationReason.NotTerminated);

        launchMock.Setup(l => l.ExecuteLaunchChainAsync(
                It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { context }.AsReadOnly()));

        inputMock.Setup(i => i.SubscribeAsync(It.IsAny<Action<RawInputEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        uiaMock.Setup(u => u.SubscribeToEventsAsync(
                It.IsAny<IReadOnlyList<TargetApplicationContext>>(),
                It.IsAny<Action<ElementInfo>>(),
                It.IsAny<Action<WindowSnapshot>>(),
                It.IsAny<Action<IntPtr>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var windowId = Guid.NewGuid();
        var testElement = new ElementInfo(
            "btn1", "button1", "OK", "Button", null, null, null,
            true, false, true, new BoundingRectangle(100, 100, 80, 30),
            ["Invoke"], null, 2, []);

        uiaMock.Setup(u => u.GetElementAtPointAsync(It.IsAny<ScreenPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElementInfo>.Success(testElement));

        var testWindow = new WindowSnapshot(
            windowId, "Calculator", 1001, "Calculator Window", "WindowClass",
            new BoundingRectangle(0, 0, 800, 600),
            DateTime.UtcNow, DateTime.UtcNow, 1, testElement,
            HierarchyRecapturePolicy.ComputeFingerprint(testElement));

        uiaMock.Setup(u => u.GetOwningWindowAsync(It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<WindowSnapshot>.Success(testWindow));

        var startResult = await service.StartRecordingAsync(CancellationToken.None);
        startResult.IsSuccess.Should().BeTrue();
        service.CurrentState.Should().Be(RecordingSessionState.Recording);

        // Step 3: Simulate some actions (normally captured via input hook + UIA correlation)
        // We'll access the session directly via the service's CurrentSession
        var session = service.CurrentSession;
        session.Should().NotBeNull();

        // Simulate a few recorded actions
        session!.Actions.Add(new RecordedAction(
            Guid.NewGuid(), 1, DateTime.UtcNow, ActionType.Click,
            "Calculator", windowId, testElement, [],
            new ScreenPoint(120, 115), null, null, null, null, null));

        session.Actions.Add(new RecordedAction(
            Guid.NewGuid(), 2, DateTime.UtcNow, ActionType.Click,
            "Calculator", windowId, testElement, [],
            new ScreenPoint(200, 150), null, null, null, null, null));

        session.Windows[windowId] = testWindow;

        // Step 4: Stop recording
        var stopResult = await service.StopSessionAsync(CancellationToken.None);
        stopResult.IsSuccess.Should().BeTrue();
        stopResult.Value.Should().Be(session);
        service.CurrentState.Should().Be(RecordingSessionState.Reviewing);

        // Step 5: Verify session summary
        var summary = service.GetSessionSummary();
        summary.Should().NotBeNull();
        summary!.ActionCount.Should().Be(2);
        summary.WindowCount.Should().Be(1);
        summary.TargetApplicationTags.Should().Contain("Calculator");
        summary.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);

        // Step 6: Export
        var exportDir = Path.Combine(Path.GetTempPath(), "E2ETestExport_" + Guid.NewGuid());
        try
        {
            exportMock.Setup(e => e.ExportSessionAsync(
                    It.IsAny<RecordingSession>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success());

            var exportResult = await service.ExportSessionAsync(exportDir, CancellationToken.None);
            exportResult.IsSuccess.Should().BeTrue();
            service.CurrentState.Should().Be(RecordingSessionState.Exported);
        }
        finally
        {
            if (Directory.Exists(exportDir))
                Directory.Delete(exportDir, recursive: true);
        }

        // Step 7: Reset for new session
        service.ResetToIdle();
        service.CurrentState.Should().Be(RecordingSessionState.Idle);
        service.CurrentSession.Should().BeNull();
    }
}