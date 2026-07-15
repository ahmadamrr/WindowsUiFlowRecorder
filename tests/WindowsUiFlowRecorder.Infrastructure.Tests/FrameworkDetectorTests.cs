namespace WindowsUiFlowRecorder.Infrastructure.Tests;

using FluentAssertions;
using WindowsUiFlowRecorder.Infrastructure.Automation;

public class FrameworkDetectorTests
{
    [Theory]
    [InlineData("WinForm", "WinForm")]
    [InlineData("WPF", "WPF")]
    [InlineData("WinUI", "WinUI")]
    [InlineData("Win32", "Win32")]
    [InlineData("WinForms", "WinForm")]
    [InlineData("WindowsForms", "WinForm")]
    [InlineData("UIA", "Win32")]
    [InlineData("UIA2", "Win32")]
    [InlineData("UIA3", "Win32")]
    [InlineData("", "Unknown")]
    public void NormalizeFrameworkId_VariousInputs_ReturnsExpected(string input, string expected)
    {
        var result = FrameworkDetector.NormalizeFrameworkId(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("WindowsForms10.Window.20808", "WinForm")]
    [InlineData("WindowsForms10.EDIT.app.0.378734", "WinForm")]
    [InlineData("WindowsForms", "WinForm")]
    [InlineData("HwndWrapper[WpfApp]", "")]
    [InlineData("", "")]
    [InlineData("NotAClassName", "")]
    public void TryDetectFromClassName_VariousPatterns_ReturnsExpected(string className, string expected)
    {
        var result = FrameworkDetector.TryDetectFromClassName(className, out var frameworkId);
        if (string.IsNullOrEmpty(expected))
        {
            result.Should().BeFalse();
        }
        else
        {
            result.Should().BeTrue();
            frameworkId.Should().Be(expected);
        }
    }
}
