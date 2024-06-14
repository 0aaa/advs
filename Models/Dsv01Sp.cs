using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Models
{
    internal class Dsv01Sp : BaseVm
    {
		private const int VALUES_CNT = 3;
        public SpeedValue[] DeviceSpeedValues { get; set; }
        public decimal SpeedValue { get; }// Скорость потока воздуха на которой снимается значение.
        public decimal?[] ReferenceSpeedValues { get; set; }// Снимаемое значение скорости с эталона.
        public decimal? ReferenceSpeedValueMain { get; set; }

        public Dsv01Sp(decimal sp)
        {
			DeviceSpeedValues = new SpeedValue[VALUES_CNT];
			ReferenceSpeedValues = new decimal?[VALUES_CNT];
            for (int i = 0; i < DeviceSpeedValues.Length; i++)
			{
				DeviceSpeedValues[i] = new SpeedValue();
            }
            SpeedValue = sp;
        }
    }
}