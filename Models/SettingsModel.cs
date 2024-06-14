using VerificationAirVelocitySensor.Model.EnumLib;
using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.Models
{
    internal class SettingsModel : BaseVm
    {
        // Настройки частотомера.
        public LpfChannel FrequencyChannel { get; set; } = LpfChannel.Channel1;
        public GateTimeSec GateTime { get; set; } = GateTimeSec.S4;
		// COM-ports.
		public string TubePort { get; set; }
		public string CymometerPort { get; set; }
		public bool[] FilterChannels { get; set; } = new bool[2];// FilterChannel1, FilterChannel2.
        public int SetFrequencyTube { get; set; } = 0;// Режим ручной настройки ПКЛ73.
    }
}