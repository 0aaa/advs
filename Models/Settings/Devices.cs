namespace ADVS.Models
{
    internal partial class Devices : ViewModels.Base.BaseVm
    {
        public Enums.Secs Sec { get; set; } = Enums.Secs.S4;// Настройки частотомера.
		// COM-ports.
		public string Tube { get; set; }
		public string Cymometer { get; set; }
    }
}