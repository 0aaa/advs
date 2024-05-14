using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.Model
{
	internal class DvsValue03 : BaseVm
	{
        public decimal SpeedValue { get; set; }

        public DvsValue03(decimal speedValue)
		{
			SpeedValue = speedValue;
		}
	}
}