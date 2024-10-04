namespace VerificationAirVelocitySensor.Views
{
    public partial class SettingsView// Логика взаимодействия для SettingsView.xaml.
    {
        internal SettingsView(ViewModels.DeviceSettingsVm s)
        {
            DataContext = s;
            InitializeComponent();
        }
    }
}