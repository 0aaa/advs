

using System;
using VerificationAirVelocitySensor.ViewModel;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.View
{
    /// <summary>
    /// Логика взаимодействия для SettingsView.xaml
    /// </summary>
    public partial class SettingsView 
    {
        public SettingsView(SettingsVm settingsVm)
        {
            DataContext = settingsVm;
            InitializeComponent();
        }
    }
}
