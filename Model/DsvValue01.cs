using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.Model
{
    internal class DsvValue01 : BaseVm
    {
		private const int VALUES_CNT = 3;
        public SpeedValue[] DeviceSpeedValues { get; set; }
        /// <summary>Скорость потока воздуха на которой снимается значение</summary>
        public decimal SpeedValue { get; }
        /// <summary>Снимаемое значение скорости с эталона</summary>
        public decimal?[] ReferenceSpeedValues { get; set; }
        public decimal? ReferenceSpeedValueMain { get; set; }

        public DsvValue01(decimal speedValue)
        {
			DeviceSpeedValues = new SpeedValue[VALUES_CNT];
			ReferenceSpeedValues = new decimal?[VALUES_CNT];
            for (int i = 0; i < DeviceSpeedValues.Length; i++)
			{
				DeviceSpeedValues[i] = new SpeedValue();
            }
            SpeedValue = speedValue;
        }
    }
}