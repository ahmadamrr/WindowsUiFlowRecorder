namespace WindowsUiFlowRecorder.Infrastructure.Input;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WindowsUiFlowRecorder.Application.Abstractions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Policies;

public class GlobalInputHook : IGlobalInputHook, IDisposable
{
    private readonly ILogger<GlobalInputHook> _logger;
    private IntPtr _keyboardHookId = IntPtr.Zero;
    private IntPtr _mouseHookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _keyboardProc;
    private LowLevelMouseProc? _mouseProc;
    private Action<RawInputEvent>? _callback;
    private Thread? _hookThread;
    private CancellationTokenSource? _threadCts;
    private bool _disposed;

    public DateTime LastHeartbeatUtc { get; private set; }

    public GlobalInputHook(ILogger<GlobalInputHook> logger)
    {
        _logger = logger;
        LastHeartbeatUtc = DateTime.UtcNow;
    }

    public Task SubscribeAsync(Action<RawInputEvent> onInputEvent, CancellationToken ct)
    {
        _callback = onInputEvent;
        _threadCts = new CancellationTokenSource();

        using var curProcess = Process.GetCurrentProcess();
        using var mainModule = curProcess.MainModule!;
        var moduleHandle = GetModuleHandle(mainModule.ModuleName);

        var setupEvent = new ManualResetEventSlim(false);
        Exception? setupException = null;

        _hookThread = new Thread(() =>
        {
            try
            {
                _keyboardProc = KeyboardHookCallback;
                _mouseProc = MouseHookCallback;

                _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
                _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, moduleHandle, 0);

                setupException = null;
                setupEvent.Set();

                if (_keyboardHookId == IntPtr.Zero && _mouseHookId == IntPtr.Zero)
                    return;

                while (!_threadCts!.Token.IsCancellationRequested)
                {
                    if (!PeekMessage(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            catch (Exception ex)
            {
                setupException = ex;
                setupEvent.Set();
                _logger.LogError(ex, "Hook thread error");
            }
            finally
            {
                UninstallHooks();
            }
        })
        {
            Name = "GlobalInputHook",
            IsBackground = true
        };

        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        if (!setupEvent.Wait(5000))
        {
            _logger.LogError("Hook thread failed to start within 5s");
            return Task.FromCanceled(ct);
        }

        if (setupException != null)
        {
            _logger.LogError(setupException, "Hook thread setup failed");
            return Task.FromException(setupException);
        }

        if (_keyboardHookId == IntPtr.Zero)
            _logger.LogError("Failed to install keyboard hook (error {Error})", Marshal.GetLastWin32Error());
        if (_mouseHookId == IntPtr.Zero)
            _logger.LogError("Failed to install mouse hook (error {Error})", Marshal.GetLastWin32Error());

        _logger.LogInformation("Global input hooks installed on dedicated thread (kb:{Kb}, mouse:{Mouse})",
            _keyboardHookId != IntPtr.Zero, _mouseHookId != IntPtr.Zero);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync()
    {
        _threadCts?.Cancel();
        _hookThread?.Join(2000);
        _hookThread = null;
        _logger.LogInformation("Global input hooks removed");
        return Task.CompletedTask;
    }

    private void UninstallHooks()
    {
        if (_keyboardHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHookId);
            _keyboardHookId = IntPtr.Zero;
        }
        if (_mouseHookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHookId);
            _mouseHookId = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            var isKeyDown = wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN;
            var isKeyUp = wParam == WM_KEYUP || wParam == WM_SYSKEYUP;

            if (isKeyDown || isKeyUp)
            {
                var evt = new RawInputEvent(
                    isKeyDown ? InputEventType.KeyDown : InputEventType.KeyUp,
                    DateTime.UtcNow, null, vkCode,
                    IsPrintableKey(vkCode), GetForegroundWindow());

                LastHeartbeatUtc = DateTime.UtcNow;
                _callback?.Invoke(evt);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            InputEventType eventType;
            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN: eventType = InputEventType.MouseDown; break;
                case WM_LBUTTONUP: eventType = InputEventType.MouseUp; break;
                case WM_RBUTTONDOWN: eventType = InputEventType.MouseDown; break;
                case WM_RBUTTONUP: eventType = InputEventType.MouseUp; break;
                case WM_MOUSEMOVE:
                    if ((hookStruct.flags & LLMHF_INJECTED) != 0)
                        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                    eventType = InputEventType.MouseMove;
                    break;
                default: return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }

            var evt = new RawInputEvent(
                eventType, DateTime.UtcNow,
                new ScreenPoint(hookStruct.pt.x, hookStruct.pt.y),
                null, false, GetForegroundWindow());

            LastHeartbeatUtc = DateTime.UtcNow;
            _callback?.Invoke(evt);
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static bool IsPrintableKey(int vkCode) =>
        (vkCode >= 0x20 && vkCode <= 0x5A) || vkCode == 0x08;

    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int LLMHF_INJECTED = 0x00000001;
    private const uint PM_REMOVE = 0x0001;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            UnsubscribeAsync().GetAwaiter().GetResult();
            _threadCts?.Dispose();
            _disposed = true;
        }
    }
}