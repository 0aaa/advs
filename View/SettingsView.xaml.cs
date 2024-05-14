using VerificationAirVelocitySensor.ViewModel;

namespace VerificationAirVelocitySensor.View
{
    /// <summary>Логика взаимодействия для SettingsView.xaml</summary>
    public partial class SettingsView
    {
        internal SettingsView(SettingsVm settingsVm)
        {
            DataContext = settingsVm;
            InitializeComponent();
        }
    }
}