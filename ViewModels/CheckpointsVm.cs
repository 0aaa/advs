using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.Models.Classes;
using VerificationAirVelocitySensor.ViewModels.Base;

namespace VerificationAirVelocitySensor.ViewModels
{
    internal class CheckpointsVm(ObservableCollection<Checkpoint> c, RelayCommand s) : BaseVm
    {
        private readonly Checkpoint[] _default = [// Array для востановления дефолтных настроек.
            new Checkpoint { Id = 1, S = 0.7m, F = 445, MaxStep = 10, Min = 0m, Max = 3.007m }
			, new Checkpoint { Id = 2, S = 5m, F = 2605, MaxStep = 20, Min = 3.320m, Max = 8.837m }
			, new Checkpoint { Id = 3, S = 10m, F = 5650, MaxStep = 20, Min = 9.634m, Max = 15.595m }
			, new Checkpoint { Id = 4, S = 15m, F = 7750, MaxStep = 20, Min = 15.935m, Max = 22.366m }
			, new Checkpoint { Id = 5, S = 20m, F = 10600, MaxStep = 30, Min = 22.248m, Max = 29.124m }
			, new Checkpoint { Id = 6, S = 25m, F = 13600, MaxStep = 30, Min = 28.549m, Max = 35.895m }
			, new Checkpoint { Id = 7, S = 30m, F = 16384, MaxStep = 30, Min = 32.340m, Max = 39.948m }
        ];
        public RelayCommand SetDefault => new(SetDefaultCps);
		public RelayCommand Save { get; } = s;
		public ObservableCollection<Checkpoint> Checkpoints { get; } = c;

        private void SetDefaultCps()
        {
            Checkpoints.Clear();
			for (int i = 0; i < _default.Length; i++)
            {
				Checkpoints.Add(_default[i]);
			}
        }
    }
}