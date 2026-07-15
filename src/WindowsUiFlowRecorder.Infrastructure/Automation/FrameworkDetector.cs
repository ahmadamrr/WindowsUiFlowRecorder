using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace WindowsUiFlowRecorder.Infrastructure.Automation;

internal static class FrameworkDetector
{
    private static readonly HashSet<string> _knownFrameworkIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "WinForm", "WPF", "WinUI", "Win32", "WinForms", "WindowsForms"
    };

    internal static string DetectFramework(AutomationElement element, int processId)
    {
        try
        {
            var frameworkId = element.Properties.FrameworkId.ValueOrDefault;
            if (!string.IsNullOrWhiteSpace(frameworkId))
                return NormalizeFrameworkId(frameworkId);

            if (TryDetectFromParentChain(element, out var parentResult))
                return parentResult;

            var className = element.ClassName ?? "";
            if (TryDetectFromClassName(className, out var classResult))
                return classResult;

            if (TryDetectFromProcessModules(processId, out var moduleResult))
                return moduleResult;

            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static bool TryDetectFromParentChain(AutomationElement element, out string frameworkId)
    {
        try
        {
            var current = element;
            var visited = 0;
            const int maxDepth = 50;

            while (current != null && visited < maxDepth)
            {
                try
                {
                    var parent = current.Parent;
                    if (parent == null)
                        break;

                    var parentFid = parent.Properties.FrameworkId.ValueOrDefault;
                    if (!string.IsNullOrWhiteSpace(parentFid))
                    {
                        frameworkId = NormalizeFrameworkId(parentFid);
                        return true;
                    }

                    current = parent;
                    visited++;
                }
                catch
                {
                    break;
                }
            }
        }
        catch
        {
        }

        frameworkId = "";
        return false;
    }

    private static bool TryDetectFromClassName(string className, out string frameworkId)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            frameworkId = "";
            return false;
        }

        if (className.StartsWith("WindowsForms10", StringComparison.OrdinalIgnoreCase) ||
            className.StartsWith("WindowsForms", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("WindowsForms"))
        {
            frameworkId = "WinForm";
            return true;
        }

        if (className.StartsWith("HwndWrapper", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("HwndWrapper"))
        {
            frameworkId = "";
            return false;
        }

        frameworkId = "";
        return false;
    }

    private static bool TryDetectFromProcessModules(int processId, out string frameworkId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var modules = process.Modules;
            if (modules == null || modules.Count == 0)
            {
                frameworkId = "";
                return false;
            }

            var foundHwndWrapper = false;

            for (var i = 0; i < modules.Count; i++)
            {
                try
                {
                    var moduleName = modules[i]?.ModuleName ?? "";
                    if (moduleName.Equals("PresentationFramework.dll", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.Equals("PresentationCore.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        frameworkId = "WPF";
                        return true;
                    }

                    if (moduleName.Equals("System.Windows.Forms.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        frameworkId = "WinForm";
                        return true;
                    }

                    if (moduleName.Equals("Microsoft.UI.Xaml.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        frameworkId = "WinUI";
                        return true;
                    }

                    if (moduleName.StartsWith("HwndWrapper", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.Equals("Windows.UI.Xaml.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        foundHwndWrapper = true;
                    }
                }
                catch
                {
                }
            }

            if (foundHwndWrapper)
            {
                frameworkId = "WinUI";
                return true;
            }
        }
        catch
        {
        }

        frameworkId = "";
        return false;
    }

    private static string NormalizeFrameworkId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Unknown";

        if (raw.Equals("WinForms", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("WindowsForms", StringComparison.OrdinalIgnoreCase))
            return "WinForm";

        if (raw.Equals("UIA", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("UIA2", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("UIA3", StringComparison.OrdinalIgnoreCase))
            return "Win32";

        return raw;
    }
}
