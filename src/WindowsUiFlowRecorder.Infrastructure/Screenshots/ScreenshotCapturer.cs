namespace WindowsUiFlowRecorder.Infrastructure.Screenshots;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class ScreenshotCapturer : IScreenshotCapturer
{
    private readonly ILogger<ScreenshotCapturer> _logger;
    private int _sequenceNumber;

    public ScreenshotCapturer(ILogger<ScreenshotCapturer> logger)
    {
        _logger = logger;
    }

    public Task<Result<ScreenshotReference>> CaptureFullScreenAsync(
        string workingFolder, CancellationToken ct)
    {
        try
        {
            var bounds = GetVirtualScreenBounds();
            using var bitmap = CaptureBitmap(bounds);
            return SaveScreenshotAsync(bitmap, workingFolder, ScreenshotScope.FullScreen, bounds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture full screen");
            return Task.FromResult(Result<ScreenshotReference>.Failure(
                FailureReason.DiskWriteFailed, ex.Message));
        }
    }

    public Task<Result<ScreenshotReference>> CaptureWindowAsync(
        IntPtr windowHandle, string workingFolder, CancellationToken ct)
    {
        try
        {
            var bounds = GetWindowBounds(windowHandle);
            using var bitmap = CaptureBitmap(bounds);
            return SaveScreenshotAsync(bitmap, workingFolder, ScreenshotScope.Window, bounds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture window");
            return Task.FromResult(Result<ScreenshotReference>.Failure(
                FailureReason.DiskWriteFailed, ex.Message));
        }
    }

    public Task<Result<ScreenshotReference>> CaptureElementAsync(
        BoundingRectangle elementBounds, string workingFolder, CancellationToken ct)
    {
        try
        {
            using var bitmap = CaptureBitmap(elementBounds);
            return SaveScreenshotAsync(bitmap, workingFolder, ScreenshotScope.Element, elementBounds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture element");
            return Task.FromResult(Result<ScreenshotReference>.Failure(
                FailureReason.DiskWriteFailed, ex.Message));
        }
    }

    private static Bitmap CaptureBitmap(BoundingRectangle bounds)
    {
        var bitmap = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height));
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0,
            new Size(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height)));
        return bitmap;
    }

    private static BoundingRectangle GetVirtualScreenBounds()
    {
        var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        return new BoundingRectangle(x, y, w, h);
    }

    private static BoundingRectangle GetWindowBounds(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out var rect))
            return new BoundingRectangle(0, 0, 1920, 1080);

        return new BoundingRectangle(
            rect.Left, rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top);
    }

    private async Task<Result<ScreenshotReference>> SaveScreenshotAsync(
        Bitmap bitmap, string workingFolder, ScreenshotScope scope,
        BoundingRectangle bounds, CancellationToken ct)
    {
        var seq = Interlocked.Increment(ref _sequenceNumber);
        var scopeName = scope switch
        {
            ScreenshotScope.FullScreen => "full",
            ScreenshotScope.Window => "window",
            ScreenshotScope.Element => "element",
            _ => "unknown"
        };

        Directory.CreateDirectory(workingFolder);
        var fileName = $"{seq:D4}_{scopeName}.png";
        var filePath = Path.Combine(workingFolder, fileName);

        await Task.Run(() =>
        {
            bitmap.Save(filePath, ImageFormat.Png);
        }, ct);

        var reference = new ScreenshotReference(
            Guid.NewGuid(), fileName, scope, ScreenshotFormat.PNG,
            bounds.Width, bounds.Height, DateTime.UtcNow,
            null, null, filePath);

        _logger.LogDebug("Saved screenshot: {File} ({W}x{H})", fileName, bounds.Width, bounds.Height);
        return Result<ScreenshotReference>.Success(reference);
    }

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}