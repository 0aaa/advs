using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class DebugVm : BaseVm.BaseVm
    {

        public RelayCommand StopFrequencyMotorCommand =>
            new RelayCommand(() => FrequencyMotorDevice.Instance.SetFrequency(0, 0),
                FrequencyMotorDevice.Instance.IsOpen);

        public RelayCommand SetSpeedFrequencyMotorCommand => new RelayCommand(SetSpeedFrequencyMotorMethod, FrequencyMotorDevice.Instance.IsOpen);

        public int SetFrequencyMotor { get; set; }

        private void SetSpeedFrequencyMotorMethod()
        {
            FrequencyMotorDevice.Instance.SetFrequency(SetFrequencyMotor, 0);
        }

        public void Unloaded()
        {
            SetSpeedFrequencyMotorMethod();
            FrequencyMotorDevice.Instance.ClosePort();
        }


    }
}
