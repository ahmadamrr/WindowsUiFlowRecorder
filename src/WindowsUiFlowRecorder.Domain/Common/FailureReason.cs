namespace WindowsUiFlowRecorder.Domain.Common;

public enum FailureReason
{
    ProcessNotStarted,
    ReadinessTimeout,
    ProcessCrashedBeforeReady,
    ElementNotFound,
    ElementStale,
    WindowNotFound,
    HierarchyTooDeep,
    ExportValidationFailed,
    DiskWriteFailed,
    SerializationFailed,
    InvalidProfile,
    InvalidLaunchChain,
    ConditionMisconfigured,
    SessionNotFound,
    ElevationMismatch,
    InputHookDisconnected,
    AutomationNotAvailable,
    OperationCanceled,
    Unknown
}