namespace WindowsUiFlowRecorder.Infrastructure.Tests;

using FluentAssertions;
using WindowsUiFlowRecorder.Infrastructure.Automation;

public class ElementPathFormatterTests
{
    [Theory]
    [InlineData("Button", "Submit", "btnOk", "Button:Submit#btnOk")]
    [InlineData("Pane", "MainPanel", "", "Pane:MainPanel")]
    [InlineData("Button", "", "submitBtn", "Button#submitBtn")]
    [InlineData("Window", "", "", "Window")]
    [InlineData("Button", null, "btnOk", "Button#btnOk")]
    [InlineData("Button", "Submit", null, "Button:Submit")]
    [InlineData("Unknown", null, null, "Unknown")]
    public void FormatEntry_VariousCombinations_ReturnsExpected(
        string controlType, string? name, string? autoId, string expected)
    {
        var result = ElementPathFormatter.FormatEntry(controlType, name, autoId);
        result.Should().Be(expected);
    }
}
