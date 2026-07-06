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

public class SessionEdgeCaseTests
{
    [Fact]
    public async Task StartRecordingAsync_LaunchChainFails_TransitionsToLaunchFailed()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await service.PrepareAsync(chain, CancellationToken.None);

        mocks.LaunchMock.Setup(l => l.ExecuteLaunchChainAsync(
                It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Failure(
                FailureReason.ReadinessTimeout, "HSM not connected"));

        var result = await service.StartRecordingAsync(CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        service.CurrentState.Should().Be(RecordingSessionState.LaunchFailed);
    }

    [Fact]
    public async Task StopSessionAsync_WithoutStarting_ReturnsFailure()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var result = await service.StopSessionAsync(CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        service.CurrentState.Should().Be(RecordingSessionState.Idle);
    }

    [Fact]
    public async Task PauseSession_WhenIdle_ReturnsFailure()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var result = service.PauseSession();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResumeSession_WhenIdle_ReturnsFailure()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var result = service.ResumeSession();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DoubleStart_ReturnsFailure()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await service.PrepareAsync(chain, CancellationToken.None);

        mocks.LaunchMock.Setup(l => l.ExecuteLaunchChainAsync(
                It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { CreateContext() }.AsReadOnly()));
        SetupHappyPath(mocks);
        await service.StartRecordingAsync(CancellationToken.None);

        var result = await service.StartRecordingAsync(CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExportSessionAsync_WithoutSession_ReturnsFailure()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var result = await service.ExportSessionAsync("/tmp/export", CancellationToken.None);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResetToIdle_ClearsAllState()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await service.PrepareAsync(chain, CancellationToken.None);
        mocks.LaunchMock.Setup(l => l.ExecuteLaunchChainAsync(
                It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { CreateContext() }.AsReadOnly()));
        SetupHappyPath(mocks);
        await service.StartRecordingAsync(CancellationToken.None);
        await service.StopSessionAsync(CancellationToken.None);

        service.ResetToIdle();

        service.CurrentState.Should().Be(RecordingSessionState.Idle);
        service.CurrentSession.Should().BeNull();
        service.GetSessionSummary().Should().BeNull();
    }

    [Fact]
    public async Task GetSessionSummary_AfterStop_ReturnsCorrectCounts()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await service.PrepareAsync(chain, CancellationToken.None);
        mocks.LaunchMock.Setup(l => l.ExecuteLaunchChainAsync(
                It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { CreateContext() }.AsReadOnly()));
        SetupHappyPath(mocks);
        await service.StartRecordingAsync(CancellationToken.None);
        await service.StopSessionAsync(CancellationToken.None);

        var summary = service.GetSessionSummary();

        summary.Should().NotBeNull();
        summary!.ActionCount.Should().Be(0);
        summary.WindowCount.Should().Be(0);
        summary.TargetApplicationTags.Should().Contain("TestApp");
    }

    [Fact]
    public async Task PauseResume_Cycle_WorksCorrectly()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);

        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await service.PrepareAsync(chain, CancellationToken.None);
        mocks.LaunchMock.Setup(l => l.ExecuteLaunchChainAsync(
                It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { CreateContext() }.AsReadOnly()));
        SetupHappyPath(mocks);
        await service.StartRecordingAsync(CancellationToken.None);

        service.CurrentState.Should().Be(RecordingSessionState.Recording);
        service.PauseSession();
        service.CurrentState.Should().Be(RecordingSessionState.Paused);
        service.ResumeSession();
        service.CurrentState.Should().Be(RecordingSessionState.Recording);
    }

    [Fact]
    public async Task StopSessionAsync_TriggersStateChangedEvents()
    {
        var mocks = CreateMocks();
        var service = CreateService(mocks);
        var states = new List<RecordingSessionState>();
        service.StateChanged += s => states.Add(s);

        var chain = new ApplicationLaunchChain([CreateLaunchStep()]);
        await service.PrepareAsync(chain, CancellationToken.None);
        mocks.LaunchMock.Setup(l => l.ExecuteLaunchChainAsync(
                It.IsAny<ApplicationLaunchChain>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<TargetApplicationContext>>.Success(
                new List<TargetApplicationContext> { CreateContext() }.AsReadOnly()));
        SetupHappyPath(mocks);
        await service.StartRecordingAsync(CancellationToken.None);
        await service.StopSessionAsync(CancellationToken.None);

        states.Should().Contain(RecordingSessionState.Recording);
        states.Should().Contain(RecordingSessionState.Reviewing);
    }

    private static (Mock<IApplicationLaunchOrchestrator> LaunchMock, Mock<IGlobalInputHook> InputMock,
        Mock<IUiAutomationProvider> UiaMock, Mock<IScreenshotCapturer> ScreenshotMock,
        Mock<IProcessLaunchMonitor> ProcessMock, Mock<IExportService> ExportMock,
        Mock<ISessionRepository> RepoMock, Mock<ISettingsService> SettingsMock) CreateMocks()
    {
        return (
            new Mock<IApplicationLaunchOrchestrator>(),
            new Mock<IGlobalInputHook>(),
            new Mock<IUiAutomationProvider>(),
            new Mock<IScreenshotCapturer>(),
            new Mock<IProcessLaunchMonitor>(),
            new Mock<IExportService>(),
            new Mock<ISessionRepository>(),
            new Mock<ISettingsService>());
    }

    private static RecordingSessionService CreateService(
        (Mock<IApplicationLaunchOrchestrator> LaunchMock, Mock<IGlobalInputHook> InputMock,
        Mock<IUiAutomationProvider> UiaMock, Mock<IScreenshotCapturer> ScreenshotMock,
        Mock<IProcessLaunchMonitor> ProcessMock, Mock<IExportService> ExportMock,
        Mock<ISessionRepository> RepoMock, Mock<ISettingsService> SettingsMock) mocks)
    {
        return new RecordingSessionService(
            mocks.LaunchMock.Object, mocks.InputMock.Object, mocks.UiaMock.Object,
            mocks.ScreenshotMock.Object, mocks.ProcessMock.Object, mocks.ExportMock.Object,
            mocks.RepoMock.Object, mocks.SettingsMock.Object,
            Mock.Of<ILogger<RecordingSessionService>>());
    }

    private static void SetupHappyPath(
        (Mock<IApplicationLaunchOrchestrator> LaunchMock, Mock<IGlobalInputHook> InputMock,
        Mock<IUiAutomationProvider> UiaMock, Mock<IScreenshotCapturer> ScreenshotMock,
        Mock<IProcessLaunchMonitor> ProcessMock, Mock<IExportService> ExportMock,
        Mock<ISessionRepository> RepoMock, Mock<ISettingsService> SettingsMock) mocks)
    {
        mocks.InputMock.Setup(i => i.SubscribeAsync(It.IsAny<Action<RawInputEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mocks.UiaMock.Setup(u => u.SubscribeToEventsAsync(It.IsAny<IReadOnlyList<TargetApplicationContext>>(),
                It.IsAny<Action<ElementInfo>>(), It.IsAny<Action<WindowSnapshot>>(),
                It.IsAny<Action<IntPtr>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        mocks.SettingsMock.Setup(s => s.GetSettingsAsync())
            .ReturnsAsync(Result<Settings>.Success(new Settings(
                ScreenshotMode.EveryAction, false, HierarchyRecaptureSensitivity.Medium,
                null, 30, 250, 5000, false, DateTime.UtcNow)));
    }

    private static LaunchStep CreateLaunchStep() => new(1, "TestApp", "test.exe", null, null,
        new ReadinessCondition(ConditionType.ProcessStarted, null, null, null, null, null, null, null, null, null),
        null, true);

    private static TargetApplicationContext CreateContext() => new(
        "TestApp", "test.exe", 12345, 1, DateTime.UtcNow, true, null, TargetTerminationReason.NotTerminated);
}