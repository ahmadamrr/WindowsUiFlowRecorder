namespace WindowsUiFlowRecorder.Presentation.Scanner;

using System.Windows;
using System.Windows.Controls;

public partial class ScannerView : UserControl
{
    public ScannerView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is ScannerViewModel vm)
                await vm.InitializeAsync();
        };
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ElementTreeNode node && DataContext is ScannerViewModel vm)
        {
            node.IsSelected = true;
            vm.SelectElementCommand.Execute(node);
        }

        if (e.OldValue is ElementTreeNode oldNode)
            oldNode.IsSelected = false;
    }
}