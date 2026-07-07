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
}