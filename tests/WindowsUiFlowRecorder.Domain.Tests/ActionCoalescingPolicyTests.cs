namespace WindowsUiFlowRecorder.Domain.Tests;

using FluentAssertions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Domain.Policies;

public class ActionCoalescingPolicyTests
{
    [Fact]
    public void Coalesce_SingleClick_ReturnsClickAction()
    {
        var events = new List<RawInputEvent>
        {
            new(InputEventType.MouseDown, DateTime.UtcNow, new ScreenPoint(100, 200), null, false, IntPtr.Zero),
            new(InputEventType.MouseUp, DateTime.UtcNow, new ScreenPoint(100, 200), null, false, IntPtr.Zero),
        };

        var element = CreateTestElement();
        var result = ActionCoalescingPolicy.Coalesce(events, element, Guid.NewGuid(), "TestApp", 1);

        result.ActionType.Should().Be(ActionType.Click);
    }

    [Fact]
    public void Coalesce_TextEntryEvents_ReturnsTextEntryAction()
    {
        var events = new List<RawInputEvent>
        {
            new(InputEventType.KeyDown, DateTime.UtcNow, null, 0x41, true, IntPtr.Zero), // 'A'
            new(InputEventType.KeyDown, DateTime.UtcNow, null, 0x42, true, IntPtr.Zero), // 'B'
        };

        var element = CreateTestElement();
        var result = ActionCoalescingPolicy.Coalesce(events, element, Guid.NewGuid(), "TestApp", 1);

        result.ActionType.Should().Be(ActionType.TextEntry);
    }

    [Fact]
    public void Coalesce_WindowActivated_ReturnsWindowActivated()
    {
        var events = new List<RawInputEvent>
        {
            new(InputEventType.WindowActivated, DateTime.UtcNow, null, null, false, IntPtr.Zero),
        };

        var element = CreateTestElement();
        var result = ActionCoalescingPolicy.Coalesce(events, element, Guid.NewGuid(), "TestApp", 1);

        result.ActionType.Should().Be(ActionType.WindowActivated);
        result.TargetElement.Should().BeNull();
    }

    [Fact]
    public void Coalesce_EmptyEvents_ThrowsArgumentException()
    {
        var element = CreateTestElement();
        Action act = () => ActionCoalescingPolicy.Coalesce([], element, Guid.NewGuid(), "TestApp", 1);
        act.Should().Throw<ArgumentException>();
    }

    private static ElementInfo CreateTestElement() => new(
        "test-id", "btnTest", "Test Button", "Button", null, null, null,
        true, false, true, new BoundingRectangle(0, 0, 100, 30),
        ["Invoke"], null, 0, 0, []);
}