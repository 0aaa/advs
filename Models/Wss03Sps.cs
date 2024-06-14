using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Models
{
	internal class Wss03Sps : BaseVm
	{
		private const int VALUES_CNT = 3;
        public decimal Speed { get; }// Скорость потока воздуха, на которой снимается значение.
		public decimal? ReferenceSpeed { get; set; }// Снимаемое значение скорости с эталона.
		public SpeedValue[] DeviceSpeeds { get; set; }

        public Wss03Sps(decimal sp)
		{
			DeviceSpeeds = new SpeedValue[VALUES_CNT];
            for (int i = 0; i < VALUES_CNT; i++)
            {
				DeviceSpeeds[i] = new SpeedValue();
            }
            Speed = sp;
		}
	}
}