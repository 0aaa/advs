using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.Models.ClassLib;
using VerificationAirVelocitySensor.ViewModels.BaseVm;

namespace VerificationAirVelocitySensor.ViewModels
{
    internal class CheckpointsVm(ObservableCollection<Checkpoint> checkpoints, RelayCommand saveCheckpoints) : BaseVm.BaseVm
    {
        private readonly Checkpoint[] _defaultCheckpoints = [// Array для востановления дефолтных настроек.
            new Checkpoint { Id = 1, Speed = 0.7m, Frequency = 445, MaxStep = 10, MinEdge = 0m, MaxEdge = 3.007m }
			, new Checkpoint { Id = 2, Speed = 5m, Frequency = 2605, MaxStep = 20, MinEdge = 3.320m, MaxEdge = 8.837m }
			, new Checkpoint { Id = 3, Speed = 10m, Frequency = 5650, MaxStep = 20, MinEdge = 9.634m, MaxEdge = 15.595m }
			, new Checkpoint { Id = 4, Speed = 15m, Frequency = 7750, MaxStep = 20, MinEdge = 15.935m, MaxEdge = 22.366m }
			, new Checkpoint { Id = 5, Speed = 20m, Frequency = 10600, MaxStep = 30, MinEdge = 22.248m, MaxEdge = 29.124m }
			, new Checkpoint { Id = 6, Speed = 25m, Frequency = 13600, MaxStep = 30, MinEdge = 28.549m, MaxEdge = 35.895m }
			, new Checkpoint { Id = 7, Speed = 30m, Frequency = 16384, MaxStep = 30, MinEdge = 32.340m, MaxEdge = 39.948m }
        ];
        public RelayCommand SetDefaultRc => new(SetDefault);
		public RelayCommand SaveCheckpointsRc { get; } = saveCheckpoints;
		public ObservableCollection<Checkpoint> Checkpoints { get; } = checkpoints;

        private void SetDefault()
        {
            Checkpoints.Clear();
			for (int i = 0; i < _defaultCheckpoints.Length; i++)
            {
				Checkpoints.Add(_defaultCheckpoints[i]);
			}
        }
    }
}