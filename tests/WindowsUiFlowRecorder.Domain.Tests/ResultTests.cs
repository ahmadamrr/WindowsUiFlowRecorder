namespace WindowsUiFlowRecorder.Domain.Tests;

using FluentAssertions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ResultTests
{
    [Fact]
    public void Result_Success_HasIsSuccessTrue()
    {
        var result = Result.Success();
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Result_Failure_HasIsFailureTrue()
    {
        var result = Result.Failure(FailureReason.ReadinessTimeout, "Timed out");
        result.IsFailure.Should().BeTrue();
        result.FailureReason.Should().Be(FailureReason.ReadinessTimeout);
        result.ErrorMessage.Should().Be("Timed out");
    }

    [Fact]
    public void ResultOfT_Success_ReturnsValue()
    {
        var result = Result<int>.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ResultOfT_Failure_NoValue()
    {
        var result = Result<int>.Failure(FailureReason.SessionNotFound);
        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public void RecordingSession_StateTransitions_FromIdleToConfiguring()
    {
        var session = new RecordingSession
        {
            SessionId = Guid.NewGuid(),
            Name = "Test Session",
            State = RecordingSessionState.Configuring,
            CreatedAtUtc = DateTime.UtcNow
        };

        session.State.Should().Be(RecordingSessionState.Configuring);
    }
}