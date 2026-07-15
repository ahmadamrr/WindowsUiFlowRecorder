using FlaUI.Core.AutomationElements;

namespace WindowsUiFlowRecorder.Infrastructure.Automation;

public static class ElementPathFormatter
{
    public static IReadOnlyList<string> BuildAncestorPath(List<AutomationElement> ancestors)
    {
        var path = new List<string>(ancestors.Count);
        foreach (var el in ancestors)
        {
            try
            {
                var ctrlType = el.ControlType.ToString() ?? "Unknown";
                var name = el.Name ?? "";
                var autoId = el.AutomationId ?? "";
                path.Add(FormatEntry(ctrlType, name, autoId));
            }
            catch
            {
                path.Add("Unknown");
            }
        }
        return path;
    }

    public static string FormatEntry(string controlType, string? name, string? automationId)
    {
        var hasName = !string.IsNullOrWhiteSpace(name);
        var hasAutoId = !string.IsNullOrWhiteSpace(automationId);

        if (hasName && hasAutoId)
            return $"{controlType}:{name}#{automationId}";

        if (hasName)
            return $"{controlType}:{name}";

        if (hasAutoId)
            return $"{controlType}#{automationId}";

        return controlType;
    }
}
