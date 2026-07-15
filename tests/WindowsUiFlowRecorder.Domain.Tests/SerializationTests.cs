namespace WindowsUiFlowRecorder.Domain.Tests;

using System.Text.Json;
using FluentAssertions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public class SerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void ApplicationProfile_RoundTrip_DeserializesCorrectly()
    {
        var original = new ApplicationProfile(
            Guid.NewGuid(),
            "Notepad",
            "Test description",
            DateTime.UtcNow,
            DateTime.UtcNow,
            new ApplicationLaunchChain([
                new LaunchStep(1, "Notepad", "notepad.exe", null, null,
                    new ReadinessCondition(ConditionType.ProcessStarted, null, null,
                        null, null, null, null, null, null, null),
                    null, true)
            ]));

        var json = JsonSerializer.Serialize(original, JsonOptions);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Notepad");
        json.Should().Contain("ProcessStarted");

        var deserialized = JsonSerializer.Deserialize<ApplicationProfile>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ProfileId.Should().Be(original.ProfileId);
        deserialized.Name.Should().Be("Notepad");
        deserialized.LaunchChain.Steps.Should().HaveCount(1);
        deserialized.LaunchChain.Steps[0].ExecutablePath.Should().Be("notepad.exe");
        deserialized.LaunchChain.Steps[0].ReadinessCondition.ConditionType.Should().Be(ConditionType.ProcessStarted);
    }

    [Fact]
    public void ApplicationProfile_RoundTrip_MultiStepChain_DeserializesCorrectly()
    {
        var original = new ApplicationProfile(
            Guid.NewGuid(),
            "Proxy + eAdmin",
            "Multi-app test",
            DateTime.UtcNow,
            DateTime.UtcNow,
            new ApplicationLaunchChain([
                new LaunchStep(1, "ProxyApp", @"C:\Proxy\ProxyApp.exe", "--hsm", null,
                    new ReadinessCondition(ConditionType.ControlPropertyEquals,
                        "Proxy*", WindowMatchMode.Contains, "lblHsmStatus", null, null,
                        ExpectedPropertyName.Value, "Connected", PropertyMatchMode.Contains, null),
                    30, true),
                new LaunchStep(2, "EAdminApp", @"C:\eAdmin\eAdminApp.exe", null, null,
                    new ReadinessCondition(ConditionType.WindowAppeared,
                        "eAdmin*", WindowMatchMode.Contains, null, null, null, null, null, null, null),
                    60, true)
            ]));

        var json = JsonSerializer.Serialize(original, JsonOptions);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("ProxyApp");
        json.Should().Contain("EAdminApp");

        var deserialized = JsonSerializer.Deserialize<ApplicationProfile>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Proxy + eAdmin");
        deserialized.LaunchChain.Steps.Should().HaveCount(2);
        deserialized.LaunchChain.Steps[0].ApplicationTag.Should().Be("ProxyApp");
        deserialized.LaunchChain.Steps[1].ApplicationTag.Should().Be("EAdminApp");
        deserialized.LaunchChain.Steps[0].ReadinessCondition.ExpectedPropertyValue.Should().Be("Connected");
    }

    [Fact]
    public void RecordedAction_WithElementPath_RoundTripsCorrectly()
    {
        var element = new ElementInfo(
            "btn1", "button1", "OK", "Button", null, null, "WinForm", null,
            true, false, false, new BoundingRectangle(100, 100, 50, 20),
            ["Invoke"], null, 1, 0, []);
        var original = new RecordedAction(
            Guid.NewGuid(), 1, DateTime.UtcNow,
            ActionType.Click, "TestApp", Guid.NewGuid(),
            element,
            ["Window:Calculator", "Pane#mainPanel", "Button:OK#button1"],
            new ScreenPoint(120, 110), null, null, null, null, null);

        var json = JsonSerializer.Serialize(original, JsonOptions);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("ElementPath");
        json.Should().Contain("Window:Calculator");
        json.Should().Contain("Pane#mainPanel");
        json.Should().Contain("Button:OK#button1");
        json.Should().Contain("WinForm");

        var deserialized = JsonSerializer.Deserialize<RecordedAction>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ElementPath.Should().HaveCount(3);
        deserialized.ElementPath.Should().Equal("Window:Calculator", "Pane#mainPanel", "Button:OK#button1");
        deserialized.TargetElement.Should().NotBeNull();
        deserialized.TargetElement!.FrameworkId.Should().Be("WinForm");
    }
}