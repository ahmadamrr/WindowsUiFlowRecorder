namespace WindowsUiFlowRecorder.Presentation.Profiles;

using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

public partial class ProfileManagerView : UserControl
{
    public ProfileManagerView()
    {
        DataContext = App.ServiceProvider.GetRequiredService<ProfileManagerViewModel>();
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is ProfileManagerViewModel vm)
                await vm.LoadProfilesAsync();
        };
    }
}