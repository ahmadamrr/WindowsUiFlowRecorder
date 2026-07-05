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
                null, 30, 250, 5000, false, DateTime.UtcNow)));

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
                null, 30, 250, 5000, false, DateTime.UtcNow)));
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
                null, 30, 250, 5000, false, DateTime.UtcNow)));
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
        Guid.NewGuid(), "TestApp", 1234, "Test Window", "WindowClass",
        new BoundingRectangle(0, 0, 800, 600),
        DateTime.UtcNow, DateTime.UtcNow, 1,
        new ElementInfo("root", null, null, "Window", null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600), [], null, 0, []),
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
    public async Task ExportStandaloneScanAsync_WritesExport()
    {
        _writerMock.Setup(w => w.WriteExportAsync(
                It.IsAny<ExportPackage>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var snapshot = CreateTestSnapshot();
        var result = await _exportService.ExportStandaloneScanAsync(snapshot, "/tmp/export", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _writerMock.Verify(w => w.WriteExportAsync(
            It.IsAny<ExportPackage>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ScreenshotReference>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static WindowSnapshot CreateTestSnapshot() => new(
        Guid.NewGuid(), "TestApp", 1234, "Test Window", "WindowClass",
        new BoundingRectangle(0, 0, 800, 600),
        DateTime.UtcNow, DateTime.UtcNow, 1,
        new ElementInfo("root", null, null, "Window", null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600), [], null, 0, []),
        new StructuralFingerprint("test"));
}