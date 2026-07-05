namespace WindowsUiFlowRecorder.Infrastructure.Processes;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;

public class ProcessLaunchMonitor : IProcessLaunchMonitor
{
    private readonly ILogger<ProcessLaunchMonitor> _logger;
    private readonly Dictionary<int, Process> _trackedProcesses = new();
    private readonly object _lock = new();

    public ProcessLaunchMonitor(ILogger<ProcessLaunchMonitor> logger)
    {
        _logger = logger;
    }

    public Task<Result<int>> StartProcessAsync(
        string executablePath, string? arguments, string? workingDirectory, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments ?? "",
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? "",
                UseShellExecute = false
            };

            var process = Process.Start(psi);
            if (process == null)
                return Task.FromResult(Result<int>.Failure(
                    FailureReason.ProcessNotStarted, "Process.Start returned null"));

            lock (_lock)
            {
                _trackedProcesses[process.Id] = process;
            }

            _logger.LogInformation("Started process {Pid}: {Path}", process.Id, executablePath);
            return Task.FromResult(Result<int>.Success(process.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process: {Path}", executablePath);
            return Task.FromResult(Result<int>.Failure(
                FailureReason.ProcessNotStarted, ex.Message));
        }
    }

    public Task<bool> IsProcessRunningAsync(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return Task.FromResult(!process.HasExited);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<Result<IReadOnlyList<IntPtr>>> EnumerateTopLevelWindowsAsync(int processId)
    {
        var windows = new List<IntPtr>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == processId && NativeMethods.IsWindowVisible(hwnd))
                windows.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        return Task.FromResult(Result<IReadOnlyList<IntPtr>>.Success(windows.AsReadOnly()));
    }

    public Task SubscribeToExitEventsAsync(
        IReadOnlyList<int> processIds, Action<int> onProcessExited)
    {
        foreach (var pid in processIds)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                process.Exited += (_, _) => onProcessExited(pid);
                process.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to subscribe to exit for process {Pid}", pid);
            }
        }
        return Task.CompletedTask;
    }

    public Task UnsubscribeAllAsync()
    {
        lock (_lock)
        {
            _trackedProcesses.Clear();
        }
        return Task.CompletedTask;
    }

    public Task KillProcessAsync(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogInformation("Killed process {Pid}", processId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill process {Pid}", processId);
        }
        return Task.CompletedTask;
    }
}