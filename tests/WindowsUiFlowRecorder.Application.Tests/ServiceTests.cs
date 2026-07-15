namespace WindowsUiFlowRecorder.Application.Tests;

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

public class RecordingSessionServiceTests
{
    private readonly Mock<IApplicationLaunchOrchestrator> _launchMock = new();
    private readonly Mock<IGlobalInputHook> _inputMock = new();
    private readonly Mock<IUiAutomationProvider> _uiaMock = new();
    private readonly Mock<IScreenshotCapturer> _screenshotMock = new();
    private readonly Mock<IProcessLaunchMonitor> _processMock = new();
    private readonly Mock<IExportService> _exportMock = new();
    private readonly Mock<ISessionRepository> _repoMock = new();
    private readonly Mock<ISettingsService> _settingsMock = new();
    private readonly Mock<ILogger<RecordingSessionService>> _loggerMock = new();
    private readonly RecordingSessionService _service;

    public RecordingSessionServiceTests()
    {
        _service = new RecordingSessionService(
            _launchMock.Object, _inputMock.Object, _uiaMock.Object,
            _screenshotMock.Object, _processMock.Object, _exportMock.Object,
            _repoMock.Object, _settingsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task PrepareAsync_WithValidInput_TransitionsToConfiguring()
    {
        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);

        var result = await _service.PrepareAsync(chain, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _service.CurrentState.Should().Be(RecordingSessionState.Configuring);
    }

    [Fact]
    public async Task StartRecordingAsync_WithMocks_TransitionsToRecording()
    {
        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await _service.PrepareAsync(chain, CancellationToken.None);

        var contexts = new List<TargetApplicationContext> { CreateContext() };
        _launchMock.Setup(l => l.ExecuteLaunchChainAsync(It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(contexts.AsReadOnly()));
        _inputMock.Setup(i => i.SubscribeAsync(It.IsAny<Action<RawInputEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uiaMock.Setup(u => u.SubscribeToEventsAsync(It.IsAny<IReadOnlyList<TargetApplicationContext>>(),
                It.IsAny<Action<ElementInfo>>(), It.IsAny<Action<WindowSnapshot>>(),
                It.IsAny<Action<IntPtr>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _settingsMock.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(Result<Settings>.Success(new Settings(
                ScreenshotMode.EveryAction, false, HierarchyRecaptureSensitivity.Medium,
                null, 30, 250, 5000, HierarchyExportScope.FullTree, false, DateTime.UtcNow)));

        var result = await _service.StartRecordingAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _service.CurrentState.Should().Be(RecordingSessionState.Recording);
    }

    [Fact]
    public async Task PauseSession_WhenRecording_TransitionsToPaused()
    {
        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await _service.PrepareAsync(chain, CancellationToken.None);
        _launchMock.Setup(l => l.ExecuteLaunchChainAsync(It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { CreateContext() }.AsReadOnly()));
        _inputMock.Setup(i => i.SubscribeAsync(It.IsAny<Action<RawInputEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uiaMock.Setup(u => u.SubscribeToEventsAsync(It.IsAny<IReadOnlyList<TargetApplicationContext>>(),
                It.IsAny<Action<ElementInfo>>(), It.IsAny<Action<WindowSnapshot>>(),
                It.IsAny<Action<IntPtr>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _settingsMock.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(Result<Settings>.Success(new Settings(
                ScreenshotMode.EveryAction, false, HierarchyRecaptureSensitivity.Medium,
                null, 30, 250, 5000, HierarchyExportScope.FullTree, false, DateTime.UtcNow)));
        await _service.StartRecordingAsync(CancellationToken.None);

        var result = _service.PauseSession();

        result.IsSuccess.Should().BeTrue();
        _service.CurrentState.Should().Be(RecordingSessionState.Paused);
    }

    [Fact]
    public void PauseSession_WhenIdle_ReturnsFailure()
    {
        var result = _service.PauseSession();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task StopSessionAsync_ReturnsStoppedSession()
    {
        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await _service.PrepareAsync(chain, CancellationToken.None);
        _launchMock.Setup(l => l.ExecuteLaunchChainAsync(It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { CreateContext() }.AsReadOnly()));
        _inputMock.Setup(i => i.SubscribeAsync(It.IsAny<Action<RawInputEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uiaMock.Setup(u => u.SubscribeToEventsAsync(It.IsAny<IReadOnlyList<TargetApplicationContext>>(),
                It.IsAny<Action<ElementInfo>>(), It.IsAny<Action<WindowSnapshot>>(),
                It.IsAny<Action<IntPtr>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _settingsMock.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(Result<Settings>.Success(new Settings(
                ScreenshotMode.EveryAction, false, HierarchyRecaptureSensitivity.Medium,
                null, 30, 250, 5000, HierarchyExportScope.FullTree, false, DateTime.UtcNow)));
        await _service.StartRecordingAsync(CancellationToken.None);

        var result = await _service.StopSessionAsync(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.State.Should().Be(RecordingSessionState.Stopped);
        _service.CurrentState.Should().Be(RecordingSessionState.Reviewing);
    }

    private static LaunchStep CreateLaunchStep() => new(
        1, "TestApp", "test.exe", null, null,
        new ReadinessCondition(ConditionType.ProcessStarted, null, null, null, null, null, null, null, null, null),
        null, true);

    private static TargetApplicationContext CreateContext() => new(
        "TestApp", "test.exe", 12345, 1, DateTime.UtcNow, true, null, TargetTerminationReason.NotTerminated);
}

public class UiScanServiceTests
{
    private readonly Mock<IUiAutomationProvider> _uiaMock = new();
    private readonly Mock<ILogger<Scanning.UiScanService>> _loggerMock = new();
    private readonly Scanning.UiScanService _scanService;

    public UiScanServiceTests()
    {
        _scanService = new Scanning.UiScanService(_uiaMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ScanWindowAsync_DelegatesToProvider()
    {
        var expected = CreateTestSnapshot();
        _uiaMock.Setup(u => u.WalkHierarchyAsync(It.IsAny<IntPtr>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<WindowSnapshot>.Success(expected));

        var result = await _scanService.ScanWindowAsync((IntPtr)1, 5000, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    private static WindowSnapshot CreateTestSnapshot() => new(
        Guid.NewGuid(), "TestApp", 1234, IntPtr.Zero, "Test Window", "WindowClass",
        new BoundingRectangle(0, 0, 800, 600),
        DateTime.UtcNow, DateTime.UtcNow, 1,
        new ElementInfo("root", null, null, "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600), [], null, 0, 0, []),
        new StructuralFingerprint("test"));
}

public class ExportServiceTests
{
    private readonly Mock<IExportWriter> _writerMock = new();
    private readonly Mock<ILogger<ExportService>> _loggerMock = new();
    private readonly ExportService _exportService;

    public ExportServiceTests()
    {
        _exportService = new ExportService(_writerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExportSessionAsync_WithValidSession_WritesExport()
    {
        _writerMock.Setup(w => w.WriteExportAsync(
                It.IsAny<ExportPackage>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        var session = CreateTestSession();

        var result = await _exportService.ExportSessionAsync(session, "/tmp/export", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _writerMock.Verify(w => w.WriteExportAsync(
            It.IsAny<ExportPackage>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ExportSessionAsync_ScreenshotReferences_ArePassedToWriter()
    {
        _writerMock.Setup(w => w.WriteExportAsync(
                It.IsAny<ExportPackage>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        var session = CreateTestSession();
        var screenshot = new ScreenshotReference(
            Guid.NewGuid(), "0001_full.png", ScreenshotScope.FullScreen,
            ScreenshotFormat.PNG, 1920, 1080, DateTime.UtcNow,
            session.Actions[0].ActionId, null, "/tmp/screenshot.png");
        session.Screenshots.Add(screenshot);

        var result = await _exportService.ExportSessionAsync(session, "/tmp/export", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _writerMock.Verify(w => w.WriteExportAsync(
            It.IsAny<ExportPackage>(), It.IsAny<string>(),
            It.Is<IReadOnlyList<ScreenshotReference>>(list => list.Count == 1),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ExportSessionAsync_InvalidSchemaVersion_ReturnsFailure()
    {
        var session = CreateTestSession();
        var package = new ExportPackage(
            "0.5.0", "0.1.0", DateTime.UtcNow,
            ExportKind.RecordingSession, null, null);

        var result = await _exportService.ExportSessionAsync(session, "/tmp/export", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.FailureReason.Should().Be(FailureReason.ExportValidationFailed);
    }

    [Fact]
    public async Task ExportStandaloneScanAsync_WithValidData_WritesExport()
    {
        _writerMock.Setup(w => w.WriteExportAsync(
                It.IsAny<ExportPackage>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        var snapshot = CreateTestSnapshot();
        var result = await _exportService.ExportStandaloneScanAsync(snapshot, "/tmp/export", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _writerMock.Verify(w => w.WriteExportAsync(
            It.IsAny<ExportPackage>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ExportStandaloneScanAsync_WithoutSnapshot_StillProducesValidPackage()
    {
        _writerMock.Setup(w => w.WriteExportAsync(
                It.IsAny<ExportPackage>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(Result.Success());

        var snapshot = CreateTestSnapshot();
        var result = await _exportService.ExportStandaloneScanAsync(snapshot, "/tmp/export", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _writerMock.Verify(w => w.WriteExportAsync(
            It.IsAny<ExportPackage>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ExportSessionAsync_ElementPathAndFrameworkId_PreservedInExport()
    {
        ExportPackage? capturedPackage = null;
        _writerMock.Setup(w => w.WriteExportAsync(
                It.IsAny<ExportPackage>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<ExportPackage, string, IReadOnlyList<ScreenshotReference>, CancellationToken, string?, string?>(
                (pkg, _, _, _, _, _) => capturedPackage = pkg)
            .ReturnsAsync(Result.Success());

        var session = CreateTestSession();

        var result = await _exportService.ExportSessionAsync(session, "/tmp/export", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        capturedPackage.Should().NotBeNull();
        var recordingExport = capturedPackage!.RecordingSession;
        recordingExport.Should().NotBeNull();

        recordingExport!.Actions.Should().HaveCount(1);
        var action = recordingExport.Actions[0];

        action.ElementPath.Should().NotBeEmpty();
        action.ElementPath.Should().Equal("Window:Calculator", "Pane#mainPanel", "Button:OK#button1");

        action.TargetElement.Should().NotBeNull();
        action.TargetElement!.FrameworkId.Should().Be("WinForm");
    }

    private static RecordingSession CreateTestSession()
    {
        var session = new RecordingSession
        {
            SessionId = Guid.NewGuid(),
            Name = "Test Session",
            CreatedAtUtc = DateTime.UtcNow,
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            StoppedAtUtc = DateTime.UtcNow,
            State = RecordingSessionState.Stopped,
            TargetApplicationContexts =
            [
                new TargetApplicationContext("TestApp", "test.exe", 12345, 1,
                    DateTime.UtcNow.AddMinutes(-5), true, null, TargetTerminationReason.NotTerminated)
            ]
        };

        session.Actions.Add(new RecordedAction(
            Guid.NewGuid(), 1, DateTime.UtcNow.AddMinutes(-4),
            ActionType.Click, "TestApp", Guid.NewGuid(),
            new ElementInfo("btn1", "button1", "OK", "Button", null, null, "WinForm", null,
                true, false, false, new BoundingRectangle(100, 100, 50, 20),
                ["Invoke"], null, 1, 0, []),
            ["Window:Calculator", "Pane#mainPanel", "Button:OK#button1"],
            new ScreenPoint(120, 110), null, null, null, null, null));

        return session;
    }

    private static WindowSnapshot CreateTestSnapshot() => new(
        Guid.NewGuid(), "TestApp", 1234, IntPtr.Zero, "Test Window", "WindowClass",
        new BoundingRectangle(0, 0, 800, 600),
        DateTime.UtcNow, DateTime.UtcNow, 1,
        new ElementInfo("root", null, null, "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600), [], null, 0, 0, []),
        new StructuralFingerprint("test"));
}