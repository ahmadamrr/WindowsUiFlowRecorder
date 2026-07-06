namespace WindowsUiFlowRecorder.Presentation.Settings;

using System.Windows.Controls;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                await vm.LoadSettingsAsync();
        };
    }
}