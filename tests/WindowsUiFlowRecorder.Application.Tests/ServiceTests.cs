namespace WindowsUiFlowRecorder.Application.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Application.Launching;
using WindowsUiFlowRecorder.Application.Recording;
using WindowsUiFlowRecorder.Application.Scanning;
using WindowsUiFlowRecorder.Application.Export;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class RecordingSessionServiceTests
{
    private readonly RecordingSessionService _service;
    private readonly Mock<ILogger<RecordingSessionService>> _loggerMock = new();

    public RecordingSessionServiceTests()
    {
        _service = new RecordingSessionService(_loggerMock.Object);
    }

    [Fact]
    public async Task StartSessionAsync_WithValidInput_TransitionsToRecording()
    {
        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        var contexts = new List<TargetApplicationContext> { CreateContext() };

        var result = await _service.StartSessionAsync(chain, contexts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _service.CurrentState.Should().Be(RecordingSessionState.Recording);
    }

    [Fact]
    public async Task PauseSession_WhenRecording_TransitionsToPaused()
    {
        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        var contexts = new List<TargetApplicationContext> { CreateContext() };
        await _service.StartSessionAsync(chain, contexts, CancellationToken.None);

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
        var contexts = new List<TargetApplicationContext> { CreateContext() };
        await _service.StartSessionAsync(chain, contexts, CancellationToken.None);

        var result = await _service.StopSessionAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.State.Should().Be(RecordingSessionState.Stopped);
        _service.CurrentState.Should().Be(RecordingSessionState.Stopped);
    }

    private static LaunchStep CreateLaunchStep() => new(
        1, "TestApp", "test.exe", null, null,
        new ReadinessCondition(ConditionType.ProcessStarted, null, null, null, null, null, null, null, null, null),
        null, true);

    private static TargetApplicationContext CreateContext() => new(
        "TestApp", "test.exe", 12345, 1, DateTime.UtcNow, true, null, TargetTerminationReason.NotTerminated);
}

public class ApplicationLaunchOrchestratorTests
{
    private readonly Mock<IProcessLaunchMonitor> _processMock = new();
    private readonly Mock<IUiAutomationProvider> _uiaMock = new();
    private readonly Mock<ILogger<ApplicationLaunchOrchestrator>> _loggerMock = new();
    private readonly ApplicationLaunchOrchestrator _orchestrator;

    public ApplicationLaunchOrchestratorTests()
    {
        _orchestrator = new ApplicationLaunchOrchestrator(
            _processMock.Object, _uiaMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_SingleStep_ReturnsContext()
    {
        _processMock.Setup(p => p.StartProcessAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(100));
        _processMock.Setup(p => p.IsProcessRunningAsync(It.IsAny<int>()))
            .ReturnsAsync(true);
        _processMock.Setup(p => p.EnumerateTopLevelWindowsAsync(It.IsAny<int>()))
            .ReturnsAsync(Result<IReadOnlyList<IntPtr>>.Success(new List<IntPtr> { (IntPtr)1 }.AsReadOnly()));

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "TestApp", "test.exe", null, null,
                new ReadinessCondition(ConditionType.ProcessStarted, null, null, null, null, null, null, null, null, null),
                null, true)
        ]);

        var result = await _orchestrator.ExecuteLaunchChainAsync(chain, 100, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].ApplicationTag.Should().Be("TestApp");
    }

    [Fact]
    public async Task ExecuteLaunchChainAsync_ProcessFailsToStart_ReturnsFailure()
    {
        _processMock.Setup(p => p.StartProcessAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure(FailureReason.ProcessNotStarted, "Access denied"));

        var chain = new ApplicationLaunchChain([
            new LaunchStep(1, "TestApp", "test.exe", null, null,
                new ReadinessCondition(ConditionType.ProcessStarted, null, null, null, null, null, null, null, null, null),
                null, true)
        ]);

        var result = await _orchestrator.ExecuteLaunchChainAsync(chain, 100, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.FailureReason.Should().Be(FailureReason.ProcessNotStarted);
    }
}

public class UiScanServiceTests
{
    private readonly Mock<IUiAutomationProvider> _uiaMock = new();
    private readonly Mock<ILogger<UiScanService>> _loggerMock = new();
    private readonly UiScanService _scanService;

    public UiScanServiceTests()
    {
        _scanService = new UiScanService(_uiaMock.Object, _loggerMock.Object);
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