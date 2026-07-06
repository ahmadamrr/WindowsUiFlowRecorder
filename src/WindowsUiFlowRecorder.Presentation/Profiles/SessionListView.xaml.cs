namespace WindowsUiFlowRecorder.Presentation.Profiles;

using System.Windows.Controls;

public partial class SessionListView : UserControl
{
    public SessionListView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is SessionListViewModel vm)
                await vm.LoadSessionsAsync();
        };
    }
}