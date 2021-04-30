using VerificationAirVelocitySensor.Model;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class SettingsVm : BaseVm.BaseVm
    {
        public SettingsModel SettingsModel { get; set; }

        public SettingsVm(SettingsModel settingsModel)
        {
            SettingsModel = settingsModel;
        }
    }
}