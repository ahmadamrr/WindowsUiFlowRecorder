namespace WindowsUiFlowRecorder.Domain.Tests;

using FluentAssertions;
using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;
using WindowsUiFlowRecorder.Domain.Policies;

public class HierarchyRecapturePolicyTests
{
    [Fact]
    public void ShouldRecapture_IdenticalFingerprints_ReturnsFalse()
    {
        var fp = new StructuralFingerprint("ABC123");
        var result = HierarchyRecapturePolicy.ShouldRecapture(fp, fp, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRecapture_DifferentFingerprintsWithinInterval_ReturnsFalse()
    {
        var result = HierarchyRecapturePolicy.ShouldRecapture(
            new StructuralFingerprint("OLD"),
            new StructuralFingerprint("NEW"),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(2));
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRecapture_DifferentFingerprintsAfterInterval_ReturnsTrue()
    {
        var result = HierarchyRecapturePolicy.ShouldRecapture(
            new StructuralFingerprint("OLD"),
            new StructuralFingerprint("NEW"),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5));
        result.Should().BeTrue();
    }

    [Fact]
    public void ComputeFingerprint_DeterministicForSameStructure()
    {
        var element = new ElementInfo(
            "root", null, null, "Window", null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600),
            [], null, 0,
        [
            new ElementInfo("btn1", "submitBtn", "Submit", "Button", null, null, null,
                true, false, true, new BoundingRectangle(10, 10, 80, 30), ["Invoke"], null, 1, [])
        ]);

        var window = new WindowSnapshot(
            Guid.NewGuid(), "TestApp", 1234, "Test Window", "WindowClass",
            new BoundingRectangle(0, 0, 800, 600),
            DateTime.UtcNow, DateTime.UtcNow, 1, element, new StructuralFingerprint(""));

        var fp1 = HierarchyRecapturePolicy.ComputeFingerprint(window);
        var fp2 = HierarchyRecapturePolicy.ComputeFingerprint(window);

        fp1.Should().Be(fp2);
    }
}