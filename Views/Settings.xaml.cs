namespace ADVS.Views
{
    public partial class SettingsView
    {
        internal SettingsView(ViewModels.DeviceSettingsVm s)
        {
            DataContext = s;
            InitializeComponent();
        }
    }
}