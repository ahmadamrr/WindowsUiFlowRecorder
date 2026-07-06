namespace WindowsUiFlowRecorder.Presentation.Shared;

using System.Windows;
using System.Windows.Interop;
using WindowsUiFlowRecorder.Domain.Common;

public partial class ElementHighlightWindow : Window
{
    public ElementHighlightWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }

    public void ShowHighlight(BoundingRectangle bounds)
    {
        Left = bounds.X;
        Top = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;
        Show();
        Topmost = true;
    }

    public void HideHighlight()
    {
        Hide();
    }
}