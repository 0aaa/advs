using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Models
{
    internal class Dvs02Sp : BaseVm
    {
		private const int VALUES_CNT = 5;
        public decimal Speed { get; }// Скорость потока воздуха, на которой снимается значение.
        public decimal? ReferenceSpeed { get; set; }// Снимаемое значение скорости с эталона.
        public SpeedValue[] DeviceSpeeds { get; set; }

        public Dvs02Sp(decimal speed)
        {
			DeviceSpeeds = new SpeedValue[VALUES_CNT];
            for (int i = 0; i < DeviceSpeeds.Length; i++)
			{
				DeviceSpeeds[i] = new SpeedValue();
            }
            Speed = speed;
        }
    }
}