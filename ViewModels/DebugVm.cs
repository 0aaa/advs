using VerificationAirVelocitySensor.Model.Lib;
using VerificationAirVelocitySensor.ViewModels.BaseVm;
using VerificationAirVelocitySensor.ViewModels.Services;

namespace VerificationAirVelocitySensor.ViewModels
{
    internal class DebugVm : BaseVm.BaseVm
    {
        public RelayCommand[] TubeCommands { get; }// StopFrequencyMotorCommand, SetSpeedFrequencyMotorCommand.
		public decimal SpeedReference { get; private set; }
        public int SetTube { get; set; }

        public DebugVm()
        {
			TubeCommands = [
				new RelayCommand(() => Tube.Instance.SetFreq(0, 0), Tube.Instance.IsOpen)
				, new RelayCommand(() => {
					Tube.Instance.ReferenceUpdate += FrequencyMotor_UpdateReferenceValue;
					Tube.Instance.SetFreq(SetTube, 0);
				}, Tube.Instance.IsOpen)
			];
		}

        private void FrequencyMotor_UpdateReferenceValue(object sender, ReferenceUpdateEventArgs e)
        {
            SpeedReference = (decimal)e.ReferenceValue;
        }

        public void Unloaded()
        {
            Tube.Instance.ReferenceUpdate -= FrequencyMotor_UpdateReferenceValue;
            Tube.Instance.SetFreq(0, 0);
            Tube.Instance.Close();
        }
    }
}