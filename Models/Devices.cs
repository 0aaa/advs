namespace VerificationAirVelocitySensor.Models
{
    internal class Devices : ViewModels.Base.BaseVm
    {
        // Настройки частотомера.
        public Enums.LpfCh Ch { get; set; } = Enums.LpfCh.Ch1;
        public Enums.Secs Sec { get; set; } = Enums.Secs.S4;
		// COM-ports.
		public string Tube { get; set; }
		public string Cymometer { get; set; }
		public bool[] FilterChs { get; set; } = new bool[2];// FilterChannel1, FilterChannel2.
        public int F { get; set; } = 0;// Режим ручной настройки ПКЛ73.
    }
}