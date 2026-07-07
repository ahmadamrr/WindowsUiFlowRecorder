namespace WindowsUiFlowRecorder.Presentation.Settings;

using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        DataContext = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                await vm.LoadSettingsAsync();
        };
    }
}