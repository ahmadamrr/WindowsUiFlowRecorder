namespace WindowsUiFlowRecorder.Domain.Common;

public readonly record struct Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public FailureReason? FailureReason { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, FailureReason? reason, string? message)
    {
        IsSuccess = isSuccess;
        FailureReason = reason;
        ErrorMessage = message;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(FailureReason reason, string? message = null)
        => new(false, reason, message);

    public void Deconstruct(out bool isSuccess, out FailureReason? reason)
    {
        isSuccess = IsSuccess;
        reason = FailureReason;
    }
}

public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public FailureReason? FailureReason { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value, FailureReason? reason, string? message)
    {
        IsSuccess = isSuccess;
        Value = value;
        FailureReason = reason;
        ErrorMessage = message;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(FailureReason reason, string? message = null)
        => new(false, default, reason, message);
}