
using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.Model;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>
    /// Пользовательские настройки
    /// </summary>
    public class UserSettings
    {
        public bool FilterChannel1 { get; set; }
        public bool FilterChannel2 { get; set; }
        public FrequencyChannel FrequencyChannel { get; set; } = FrequencyChannel.Channel1;
        public GateTime GateTime { get; set; } = GateTime.S1;
        public string ComPortFrequencyMotor { get; set; } = string.Empty;
        public string ComPortFrequencyCounter { get; set; } = string.Empty;
        public TypeTest TypeTest { get; set; }
        public ObservableCollection<SpeedPoint> SpeedPointsList { get; set; } = new ObservableCollection<SpeedPoint>();
        /// <summary>
        /// Путь сохранения результата
        /// </summary>
        public string PathSave { get; set; }

        public MeasurementsData MeasurementsData { get; set; }
    }
}