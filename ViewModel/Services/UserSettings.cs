
using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.Model;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>
    /// Пользовательские настройки
    /// </summary>
    public class UserSettings
    {
        public SettingsModel SettingsModel { get; set; } = new SettingsModel();
        public TypeTest TypeTest { get; set; } = TypeTest.Dvs01;
        public ObservableCollection<SpeedPoint> SpeedPointsList { get; set; } = new ObservableCollection<SpeedPoint>();
        /// <summary>
        /// Путь сохранения результата
        /// </summary>
        public string PathSave { get; set; }

        public MeasurementsData MeasurementsData { get; set; }
    }
}