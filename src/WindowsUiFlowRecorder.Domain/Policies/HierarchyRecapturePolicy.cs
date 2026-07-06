namespace WindowsUiFlowRecorder.Domain.Policies;

using WindowsUiFlowRecorder.Domain.Common;
using WindowsUiFlowRecorder.Domain.Entities;

public static class HierarchyRecapturePolicy
{
    public static bool ShouldRecapture(
        StructuralFingerprint previousFingerprint,
        StructuralFingerprint currentFingerprint,
        TimeSpan minimumInterval,
        TimeSpan timeSinceLastCapture)
    {
        if (timeSinceLastCapture < minimumInterval) return false;
        return previousFingerprint != currentFingerprint;
    }

    public static StructuralFingerprint ComputeFingerprint(WindowSnapshot snapshot)
        => ComputeFingerprint(snapshot.RootElement);

    public static StructuralFingerprint ComputeFingerprint(ElementInfo rootElement)
    {
        var hash = ComputeFingerprintHash(rootElement, 0);
        return new StructuralFingerprint(hash);
    }

    private static string ComputeFingerprintHash(ElementInfo element, int depth)
    {
        var childHashes = element.Children
            .Select(c => ComputeFingerprintHash(c, depth + 1))
            .ToList();

        var parts = new List<string> { element.ControlType, depth.ToString() };
        if (element.AutomationId != null) parts.Add(element.AutomationId);
        parts.AddRange(childHashes);

        var combined = string.Join("|", parts);
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(combined)));
    }
}