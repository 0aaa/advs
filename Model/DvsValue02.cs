using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.Model
{
    internal class DvsValue02 : BaseVm
    {
		private const int VALUES_CNT = 5;
        /// <summary>Скорость потока воздуха на которой снимается значение</summary>
        public decimal SpeedValue { get; }
        /// <summary>Снимаемое значение скорости с эталона</summary>
        public decimal? ReferenceSpeedValue { get; set; }
        public SpeedValue[] DeviceSpeedValues { get; set; }

        public DvsValue02(decimal speedValue)
        {
			DeviceSpeedValues = new SpeedValue[VALUES_CNT];
            for (int i = 0; i < DeviceSpeedValues.Length; i++)
			{
				DeviceSpeedValues[i] = new SpeedValue();
            }
            SpeedValue = speedValue;
        }
    }
}