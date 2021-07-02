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
        public decimal SpeedReferenceValue { get; set; }

        private void SetSpeedFrequencyMotorMethod()
        {
            FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;
            FrequencyMotorDevice.Instance.SetFrequency(SetFrequencyMotor, 0);
        }

        private void FrequencyMotor_UpdateReferenceValue(object sender, UpdateReferenceValueEventArgs e)
        {
            SpeedReferenceValue = (decimal)e.ReferenceValue;
        }

        public void Unloaded()
        {
            FrequencyMotorDevice.Instance.UpdateReferenceValue -= FrequencyMotor_UpdateReferenceValue;
            FrequencyMotorDevice.Instance.SetFrequency(0, 0);
            FrequencyMotorDevice.Instance.ClosePort();
        }


    }
}
