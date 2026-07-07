namespace WindowsUiFlowRecorder.Presentation.Profiles;

using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

public partial class SessionListView : UserControl
{
    public SessionListView()
    {
        DataContext = App.ServiceProvider.GetRequiredService<SessionListViewModel>();
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is SessionListViewModel vm)
                await vm.LoadSessionsAsync();
        };
    }
}