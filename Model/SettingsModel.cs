using VerificationAirVelocitySensor.ViewModel.BaseVm;
using VerificationAirVelocitySensor.ViewModel.Services;

namespace VerificationAirVelocitySensor.Model
{
    internal class SettingsModel : BaseVm
    {
        // Настройки частотомера.
        public FrequencyChannel FrequencyChannel { get; set; } = FrequencyChannel.Channel1;
        public GateTime GateTime { get; set; } = GateTime.S4;
        // COM-ports.
        public string ComPortFrequencyMotor { get; set; }
        public string ComPortFrequencyCounter { get; set; }
		public bool[] FilterChannels { get; set; } = new bool[2];// FilterChannel1, FilterChannel2.
        // Режим ручной настройки ПКЛ73.
        public int SetFrequencyMotor { get; set; } = 0;
    }
}