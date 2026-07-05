namespace WindowsUiFlowRecorder.Application.Launching;

using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ApplicationLaunchOrchestrator : IApplicationLaunchOrchestrator
{
    private readonly IProcessLaunchMonitor _processMonitor;
    private readonly IUiAutomationProvider _uiAutomation;
    private readonly ILogger<ApplicationLaunchOrchestrator> _logger;

    public ApplicationLaunchOrchestrator(
        IProcessLaunchMonitor processMonitor,
        IUiAutomationProvider uiAutomation,
        ILogger<ApplicationLaunchOrchestrator> logger)
    {
        _processMonitor = processMonitor;
        _uiAutomation = uiAutomation;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<TargetApplicationContext>>> ExecuteLaunchChainAsync(
        ApplicationLaunchChain chain,
        int pollIntervalMs,
        CancellationToken ct)
    {
        var contexts = new List<TargetApplicationContext>();

        foreach (var step in chain.Steps)
        {
            _logger.LogInformation("Launching step {StepOrder}: {Tag}", step.StepOrder, step.ApplicationTag);

            var startResult = await _processMonitor.StartProcessAsync(
                step.ExecutablePath, step.Arguments, step.WorkingDirectory, ct);

            if (!startResult.IsSuccess)
                return CleanupAndFail(contexts, startResult.FailureReason!.Value, startResult.ErrorMessage);

            var pid = startResult.Value!;
            var launchedAt = DateTime.UtcNow;

            var timeoutMs = (step.ReadinessTimeoutSecondsOverride ?? 30) * 1000;
            var readinessResult = await PollReadinessAsync(step, pid, pollIntervalMs, timeoutMs, ct);

            if (!readinessResult.IsSuccess)
            {
                await CleanupProcessesAsync(contexts, step.CleanUpOnFailure);
                return Result<IReadOnlyList<TargetApplicationContext>>.Failure(
                    readinessResult.FailureReason!.Value, readinessResult.ErrorMessage);
            }

            var context = new TargetApplicationContext(
                step.ApplicationTag, step.ExecutablePath, pid, step.StepOrder,
                launchedAt, true, null, TargetTerminationReason.NotTerminated);

            contexts.Add(context);
            _logger.LogInformation("Step {StepOrder} ({Tag}) ready", step.StepOrder, step.ApplicationTag);
        }

        return Result<IReadOnlyList<TargetApplicationContext>>.Success(contexts.AsReadOnly());
    }

    private async Task<Result> PollReadinessAsync(
        LaunchStep step, int pid, int pollIntervalMs, int timeoutMs, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var unexpectedErrorCount = 0;
        const int maxUnexpectedErrors = 5;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                var running = await _processMonitor.IsProcessRunningAsync(pid);
                if (!running)
                    return Result.Failure(FailureReason.ProcessCrashedBeforeReady,
                        $"Process {step.ApplicationTag} exited before readiness condition was met");

                var windows = await _processMonitor.EnumerateTopLevelWindowsAsync(pid);
                if (!windows.IsSuccess || windows.Value!.Count == 0)
                {
                    await Task.Delay(pollIntervalMs, ct);
                    continue;
                }

                var conditionMet = await EvaluateConditionAsync(step, windows.Value!, ct);
                if (conditionMet.IsSuccess)
                    return Result.Success();

                unexpectedErrorCount = 0;
            }
            catch (TaskCanceledException)
            {
                return Result.Failure(FailureReason.OperationCanceled, "Launch chain cancelled");
            }
            catch (Exception ex)
            {
                unexpectedErrorCount++;
                _logger.LogWarning(ex, "Unexpected error polling readiness for {Tag} (attempt {Count})",
                    step.ApplicationTag, unexpectedErrorCount);

                if (unexpectedErrorCount >= maxUnexpectedErrors)
                    return Result.Failure(FailureReason.ReadinessTimeout,
                        $"Too many unexpected errors polling {step.ApplicationTag}: {ex.Message}");
            }

            await Task.Delay(pollIntervalMs, ct);
        }

        return Result.Failure(FailureReason.ReadinessTimeout,
            $"Step {step.ApplicationTag} did not become ready within {timeoutMs}ms");
    }

    private async Task<Result> EvaluateConditionAsync(
        LaunchStep step, IReadOnlyList<IntPtr> windows, CancellationToken ct)
    {
        return step.ReadinessCondition.ConditionType switch
        {
            ConditionType.ProcessStarted => Result.Success(),
            ConditionType.WindowAppeared => Result.Success(),
            ConditionType.ControlPresent or ConditionType.ControlPropertyEquals =>
                await EvaluateControlConditionAsync(step, windows, ct),
            ConditionType.FixedTimeout => Result.Success(),
            _ => Result.Failure(FailureReason.ConditionMisconfigured, "Unknown condition type")
        };
    }

    private async Task<Result> EvaluateControlConditionAsync(
        LaunchStep step, IReadOnlyList<IntPtr> windows, CancellationToken ct)
    {
        foreach (var hwnd in windows)
        {
            var scan = await _uiAutomation.WalkHierarchyAsync(hwnd, 5000, ct);
            if (!scan.IsSuccess) continue;

            var match = FindMatchingElement(scan.Value!.RootElement, step.ReadinessCondition);
            if (match == null) continue;

            if (step.ReadinessCondition.ConditionType == ConditionType.ControlPropertyEquals
                && step.ReadinessCondition.ExpectedPropertyValue != null)
            {
                var actual = step.ReadinessCondition.ExpectedPropertyName switch
                {
                    ExpectedPropertyName.Value => match.ValueOrText,
                    ExpectedPropertyName.Name => match.Name,
                    ExpectedPropertyName.Text => match.ValueOrText,
                    _ => match.Name
                };

                var mode = step.ReadinessCondition.PropertyMatchMode ?? PropertyMatchMode.Exact;
                if (Matches(actual, step.ReadinessCondition.ExpectedPropertyValue, mode))
                    return Result.Success();
            }
            else
            {
                return Result.Success();
            }
        }

        return Result.Failure(FailureReason.ElementNotFound, "Condition not yet met");
    }

    private static ElementInfo? FindMatchingElement(ElementInfo root, ReadinessCondition condition)
    {
        if (MatchesElement(root, condition)) return root;
        foreach (var child in root.Children)
        {
            var match = FindMatchingElement(child, condition);
            if (match != null) return match;
        }
        return null;
    }

    private static bool MatchesElement(ElementInfo element, ReadinessCondition condition)
    {
        if (condition.ElementAutomationId != null &&
            !string.Equals(element.AutomationId, condition.ElementAutomationId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (condition.ElementName != null &&
            !string.Equals(element.Name, condition.ElementName, StringComparison.OrdinalIgnoreCase))
            return false;
        if (condition.ElementControlType != null &&
            !string.Equals(element.ControlType, condition.ElementControlType, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool Matches(string? actual, string expected, PropertyMatchMode mode)
    {
        if (actual == null) return false;
        return mode switch
        {
            PropertyMatchMode.Exact => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            PropertyMatchMode.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            PropertyMatchMode.Regex => System.Text.RegularExpressions.Regex.IsMatch(actual, expected),
            _ => false
        };
    }

    private static Result<IReadOnlyList<TargetApplicationContext>> CleanupAndFail(
        List<TargetApplicationContext> contexts, FailureReason reason, string? message)
    {
        return Result<IReadOnlyList<TargetApplicationContext>>.Failure(reason, message);
    }

    private async Task CleanupProcessesAsync(List<TargetApplicationContext> contexts, bool cleanUp)
    {
        if (!cleanUp) return;
        foreach (var ctx in contexts)
        {
            try { await _processMonitor.KillProcessAsync(ctx.ProcessId); }
            catch { _logger.LogWarning("Failed to kill process {Pid}", ctx.ProcessId); }
        }
    }
}