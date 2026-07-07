namespace WindowsUiFlowRecorder.Presentation.Recorder;

using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

public partial class RecorderView : UserControl
{
    public RecorderView()
    {
        DataContext = App.ServiceProvider.GetRequiredService<RecorderViewModel>();
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is RecorderViewModel vm)
        {
            await vm.LoadProfilesAsync();
        }
    }
}