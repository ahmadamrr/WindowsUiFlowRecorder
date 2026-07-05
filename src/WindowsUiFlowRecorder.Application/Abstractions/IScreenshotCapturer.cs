namespace WindowsUiFlowRecorder.Application.Abstractions;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public interface IScreenshotCapturer
{
    Task<Result<ScreenshotReference>> CaptureFullScreenAsync(string workingFolder, CancellationToken ct);
    Task<Result<ScreenshotReference>> CaptureWindowAsync(IntPtr windowHandle, string workingFolder, CancellationToken ct);
    Task<Result<ScreenshotReference>> CaptureElementAsync(BoundingRectangle elementBounds, string workingFolder, CancellationToken ct);
}