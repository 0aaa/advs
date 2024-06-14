using VerificationAirVelocitySensor.ViewModels;

namespace VerificationAirVelocitySensor.Views
{
    public partial class SettingsView// Логика взаимодействия для SettingsView.xaml.
    {
        internal SettingsView(SettingsVm settingsVm)
        {
            DataContext = settingsVm;
            InitializeComponent();
        }
    }
}