using System.Collections.ObjectModel;
using VerificationAirVelocitySensor.Model;

namespace VerificationAirVelocitySensor.ViewModel.Services
{
    /// <summary>Пользовательские настройки</summary>
    internal class UserSettings
    {
        public ObservableCollection<SpeedPoint> SpeedPointsList { get; set; } = new ObservableCollection<SpeedPoint>();
        public SettingsModel SettingsModel { get; set; } = new SettingsModel();
        public MeasurementsData MeasurementsData { get; set; }
        public TypeTest TypeTest { get; set; } = TypeTest.Dvs01;
        /// <summary>Путь сохранения результата</summary>
        public string PathSave { get; set; }
    }
}