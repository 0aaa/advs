using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.ViewModel
{
    internal class DebugVm : BaseVm.BaseVm
    {
        public RelayCommand[] FrequencyMotorCommands { get; }// StopFrequencyMotorCommand, SetSpeedFrequencyMotorCommand.
		public decimal SpeedReferenceValue { get; set; }
        public int SetFrequencyMotor { get; set; }

        public DebugVm()
        {
			FrequencyMotorCommands = new RelayCommand[] {
				new RelayCommand(() => FrequencyMotorDevice.Instance.SetFrequency(0, 0), FrequencyMotorDevice.Instance.IsOpen)
				, new RelayCommand(() => {
					FrequencyMotorDevice.Instance.UpdateReferenceValue += FrequencyMotor_UpdateReferenceValue;
					FrequencyMotorDevice.Instance.SetFrequency(SetFrequencyMotor, 0);
				}, FrequencyMotorDevice.Instance.IsOpen)
			};
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