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
            "root", null, null, "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600),
            [], null, 0, 0,
        [
            new ElementInfo("btn1", "submitBtn", "Submit", "Button", null, null, null, null,
                true, false, true, new BoundingRectangle(10, 10, 80, 30), ["Invoke"], null, 1, 0, [])
        ]);

        var fp1 = HierarchyRecapturePolicy.ComputeFingerprint(element);
        var fp2 = HierarchyRecapturePolicy.ComputeFingerprint(element);

        fp1.Should().Be(fp2);
    }

    [Fact]
    public void ComputeFingerprint_DifferentStructures_ReturnsDifferentHashes()
    {
        var elementA = new ElementInfo(
            "root", null, null, "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600),
            [], null, 0, 0,
        [
            new ElementInfo("btn1", "submitBtn", "Submit", "Button", null, null, null, null,
                true, false, true, new BoundingRectangle(10, 10, 80, 30), ["Invoke"], null, 1, 0, [])
        ]);

        var elementB = new ElementInfo(
            "root", null, null, "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600),
            [], null, 0, 0,
        [
            new ElementInfo("btn1", "submitBtn", "Submit", "Button", null, null, null, null,
                true, false, true, new BoundingRectangle(10, 10, 80, 30), ["Invoke"], null, 1, 0,
            [
                new ElementInfo("inner1", "childEdit", "Child", "Edit", null, null, null, null,
                    true, false, true, new BoundingRectangle(20, 20, 200, 20), ["Value"], null, 2, 0, [])
            ])
        ]);

        var fpA = HierarchyRecapturePolicy.ComputeFingerprint(elementA);
        var fpB = HierarchyRecapturePolicy.ComputeFingerprint(elementB);

        fpA.Should().NotBe(fpB);
    }

    [Fact]
    public void ComputeFingerprint_AutomationIdChange_ChangesHash()
    {
        var elementWithIdA = new ElementInfo(
            "btn1", "oldId", "OK", "Button", null, null, null, null,
            true, false, true, new BoundingRectangle(10, 10, 80, 30), ["Invoke"], null, 0, 0, []);

        var elementWithIdB = new ElementInfo(
            "btn1", "newId", "OK", "Button", null, null, null, null,
            true, false, true, new BoundingRectangle(10, 10, 80, 30), ["Invoke"], null, 0, 0, []);

        var fpA = HierarchyRecapturePolicy.ComputeFingerprint(elementWithIdA);
        var fpB = HierarchyRecapturePolicy.ComputeFingerprint(elementWithIdB);

        fpA.Should().NotBe(fpB);
    }

    [Fact]
    public void ComputeFingerprint_NestedElementAdded_ChangesHash()
    {
        var flat = new ElementInfo(
            "root", null, null, "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600),
            [], null, 0, 0, []);

        var nested = new ElementInfo(
            "root", null, null, "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 800, 600),
            [], null, 0, 0,
        [
            new ElementInfo("child1", null, null, "Pane", null, null, null, null,
                true, false, false, new BoundingRectangle(0, 0, 800, 600), [], null, 1, 0, [])
        ]);

        var fpFlat = HierarchyRecapturePolicy.ComputeFingerprint(flat);
        var fpNested = HierarchyRecapturePolicy.ComputeFingerprint(nested);

        fpFlat.Should().NotBe(fpNested);
    }

    [Fact]
    public void ComputeFingerprint_LargeHierarchy_PerformanceWithinBudget()
    {
        var root = BuildDeepHierarchy(100);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fp = HierarchyRecapturePolicy.ComputeFingerprint(root);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
        fp.Value.Should().NotBeNullOrEmpty();
    }

    private static ElementInfo BuildDeepHierarchy(int breadth)
    {
        var children = Enumerable.Range(0, breadth)
            .Select(i => new ElementInfo(
                $"child{i}", $"autoId{i}", $"Name{i}", "Button", null, null, null, null,
                true, false, true, new BoundingRectangle(i * 10, 0, 80, 30),
                ["Invoke"], null, 1, 0, []))
            .ToList();

        return new ElementInfo(
            "root", null, "Root", "Window", null, null, null, null,
            true, false, false, new BoundingRectangle(0, 0, 1024, 768),
            [], null, 0, 0, children);
    }
}