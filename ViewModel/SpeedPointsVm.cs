using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.ViewModel.BaseVm;

namespace VerificationAirVelocitySensor.ViewModel
{
    public class SpeedPointsVm : BaseVm.BaseVm
    {

        public RelayCommand SetDefaultSpeedPointsCommand => new RelayCommand(SetDefaultSpeedPoints);
        public RelayCommand SaveSpeedsPointCommand { get; set; }


        /// <summary>
        /// коллекция для востановления дефолтных настроек
        /// </summary>
        private readonly ObservableCollection<SpeedPoint> _defaultSpeedPoints = new ObservableCollection<SpeedPoint>
        {
            new SpeedPoint
                {Id = 1, Speed = 0.7m, SetFrequency = 445, MaxStep = 10, MinEdge = 0m, MaxEdge = 3.007m},
            new SpeedPoint
                {Id = 2, Speed = 5m, SetFrequency = 2605, MaxStep = 20, MinEdge = 3.320m, MaxEdge = 8.837m},
            new SpeedPoint
                {Id = 3, Speed = 10m, SetFrequency = 5650, MaxStep = 20, MinEdge = 9.634m, MaxEdge = 15.595m},
            new SpeedPoint
                {Id = 4, Speed = 15m, SetFrequency = 7750, MaxStep = 20, MinEdge = 15.935m, MaxEdge = 22.366m},
            new SpeedPoint
                {Id = 5, Speed = 20m, SetFrequency = 10600, MaxStep = 30, MinEdge = 22.248m, MaxEdge = 29.124m},
            new SpeedPoint
                {Id = 6, Speed = 25m, SetFrequency = 13600, MaxStep = 30, MinEdge = 28.549m, MaxEdge = 35.895m},
            new SpeedPoint
                {Id = 7, Speed = 30m, SetFrequency = 16384, MaxStep = 30, MinEdge = 32.340m, MaxEdge = 39.948m}
        };

        public ObservableCollection<SpeedPoint> SpeedPointsList { get; set; }

        public SpeedPointsVm(ObservableCollection<SpeedPoint> speedPointsList , RelayCommand saveSpeedsPointCommand)
        {
            SpeedPointsList = speedPointsList;
            SaveSpeedsPointCommand = saveSpeedsPointCommand;
        }

        private void SetDefaultSpeedPoints()
        {
            SpeedPointsList.Clear();

            foreach (var defaultSpeedPoint in _defaultSpeedPoints)
                SpeedPointsList.Add(defaultSpeedPoint);
        }

    }
}
