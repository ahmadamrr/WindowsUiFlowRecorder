namespace WindowsUiFlowRecorder.Domain.Common;

public enum RecordingSessionState
{
    Idle,
    Configuring,
    LaunchingChain,
    Recording,
    Paused,
    Stopped,
    Reviewing,
    Exporting,
    Exported,
    LaunchFailed
}