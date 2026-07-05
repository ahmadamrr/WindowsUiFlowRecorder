namespace WindowsUiFlowRecorder.Domain.Policies;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public static class ActionCoalescingPolicy
{
    public static RecordedAction Coalesce(
        IReadOnlyList<RawInputEvent> events,
        ElementInfo targetElement,
        Guid windowId,
        string applicationTag,
        int sequenceNumber)
    {
        if (events.Count == 0)
            throw new ArgumentException("Cannot coalesce zero events", nameof(events));

        var first = events[0];
        var last = events[^1];
        var now = last.TimestampUtc;

        if (IsDragGesture(events))
        {
            return new RecordedAction(
                Guid.NewGuid(), sequenceNumber, now,
                ActionType.Drag, applicationTag, windowId,
                targetElement, [], last.ScreenPosition,
                first.ScreenPosition, null, null, null, null);
        }

        if (IsTextEntry(events))
        {
            var text = ExtractText(events);
            return new RecordedAction(
                Guid.NewGuid(), sequenceNumber, now,
                ActionType.TextEntry, applicationTag, windowId,
                targetElement, [], null, null, text, null, null, null);
        }

        if (IsClick(events))
        {
            return new RecordedAction(
                Guid.NewGuid(), sequenceNumber, now,
                ActionType.Click, applicationTag, windowId,
                targetElement, [], last.ScreenPosition,
                null, null, null, null, null);
        }

        if (first.EventType == InputEventType.WindowActivated)
        {
            return new RecordedAction(
                Guid.NewGuid(), sequenceNumber, now,
                ActionType.WindowActivated, applicationTag, windowId,
                null, [], null, null, null, null, null, null);
        }

        if (IsKeyPress(events))
        {
            var keyName = ExtractKeyName(events);
            return new RecordedAction(
                Guid.NewGuid(), sequenceNumber, now,
                ActionType.KeyPress, applicationTag, windowId,
                targetElement, [], null, null, null, keyName, null, null);
        }

        return new RecordedAction(
            Guid.NewGuid(), sequenceNumber, now,
            ActionType.FocusChanged, applicationTag, windowId,
            targetElement, [], null, null, null, null, null, null);
    }

    private static bool IsClick(IReadOnlyList<RawInputEvent> events) =>
        events.Any(e => e.EventType is InputEventType.MouseDown or InputEventType.MouseUp) &&
        !events.Any(e => e.EventType == InputEventType.MouseMove);

    private static bool IsDragGesture(IReadOnlyList<RawInputEvent> events)
    {
        if (events.Count < 3) return false;
        var hasMouseDown = events.Any(e => e.EventType == InputEventType.MouseDown);
        var hasMouseUp = events.Any(e => e.EventType == InputEventType.MouseUp);
        var hasMove = events.Any(e => e.EventType == InputEventType.MouseMove);
        return hasMouseDown && hasMouseUp && hasMove;
    }

    private static bool IsTextEntry(IReadOnlyList<RawInputEvent> events) =>
        events.Any(e => e.IsPrintableKey);

    private static bool IsKeyPress(IReadOnlyList<RawInputEvent> events) =>
        events.Any(e => e.EventType is InputEventType.KeyDown or InputEventType.KeyUp && !e.IsPrintableKey);

    private static string ExtractText(IReadOnlyList<RawInputEvent> events) =>
        string.Concat(events
            .Where(e => e.IsPrintableKey && e.VirtualKeyCode.HasValue)
            .Select(e => (char)e.VirtualKeyCode.Value));

    private static string ExtractKeyName(IReadOnlyList<RawInputEvent> events)
    {
        var first = events.FirstOrDefault(e =>
            e.EventType is InputEventType.KeyDown or InputEventType.KeyUp && !e.IsPrintableKey);
        return first.VirtualKeyCode switch
        {
            13 => "Enter",
            9 => "Tab",
            27 => "Escape",
            112 => "F1",
            113 => "F2",
            114 => "F3",
            115 => "F4",
            116 => "F5",
            117 => "F6",
            118 => "F7",
            119 => "F8",
            120 => "F9",
            121 => "F10",
            122 => "F11",
            123 => "F12",
            var vk => vk.HasValue ? $"VK_{vk.Value}" : "Unknown"
        };
    }
}

public readonly record struct RawInputEvent(
    InputEventType EventType,
    DateTime TimestampUtc,
    ScreenPoint? ScreenPosition,
    int? VirtualKeyCode,
    bool IsPrintableKey,
    IntPtr? WindowHandle
);