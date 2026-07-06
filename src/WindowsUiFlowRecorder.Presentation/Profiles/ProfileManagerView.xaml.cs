namespace WindowsUiFlowRecorder.Presentation.Profiles;

using System.Windows.Controls;

public partial class ProfileManagerView : UserControl
{
    public ProfileManagerView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is ProfileManagerViewModel vm)
                await vm.LoadProfilesAsync();
        };
    }
}